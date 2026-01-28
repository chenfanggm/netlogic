using System;
using System.Buffers;
using System.Collections.Generic;
using Net;

namespace Sim.Server.Reliability
{
    /// <summary>
    /// Per-client reliable stream:
    /// - Coalesces ops into a single ServerOpsMsg per tick (or when forced)
    /// - Tracks acked seq and resend window
    /// - Stores pending packets using pooled byte[] (returned on ack)
    /// </summary>
    public sealed class ServerReliableStream
    {
        private readonly int _maxOpsPayloadBytes;
        private readonly int _maxPendingPackets;

        private readonly Queue<PendingPacket> _pending;
        private PendingBuild _build;

        public uint LastAckedSeq { get; private set; }
        public uint NextSeq { get; private set; }

        // Reused packet builder buffer (per stream) to avoid per-flush allocations
        private readonly ArrayBufferWriter<byte> _packetWriter = new ArrayBufferWriter<byte>(1024);

        public ServerReliableStream(int maxOpsPayloadBytes, int maxPendingPackets)
        {
            _maxOpsPayloadBytes = maxOpsPayloadBytes;
            _maxPendingPackets = maxPendingPackets;

            _pending = new Queue<PendingPacket>(maxPendingPackets);
            _build = new PendingBuild();

            LastAckedSeq = 0;
            NextSeq = 1;
        }

        public void OnAck(uint lastAckedSeq)
        {
            if (lastAckedSeq > LastAckedSeq)
                LastAckedSeq = lastAckedSeq;

            // Drop acked from pending window + return pooled buffers
            while (_pending.Count > 0 && _pending.Peek().Seq <= LastAckedSeq)
            {
                PendingPacket p = _pending.Dequeue();
                if (p.PacketBytes != null)
                    ArrayPool<byte>.Shared.Return(p.PacketBytes, clearArray: false);
            }
        }

        /// <summary>
        /// Adds an already-encoded op blob into this tick's coalesced buffer.
        /// opsPayload is expected to be a pooled buffer (rent) with valid length opsLen.
        /// This stream will own and eventually return it to pool.
        /// </summary>
        public void AddOpsForTick(int serverTick, ushort opCount, byte[] opsPayload, int opsLen)
        {
            if (opsPayload == null || opsLen <= 0)
                opsPayload = Array.Empty<byte>();

            if (_build.HasTick && _build.Tick != serverTick)
                throw new InvalidOperationException("AddOpsForTick called with different tick without flushing.");

            if (!_build.HasTick)
            {
                _build.HasTick = true;
                _build.Tick = serverTick;
            }

            int addBytes = (opsPayload == Array.Empty<byte>()) ? 0 : opsLen;

            int newSize = _build.PayloadBytes + addBytes;
            if (newSize > _maxOpsPayloadBytes)
                throw new InvalidOperationException("Reliable ops payload exceeded max bytes. Flush before adding more ops.");

            if (addBytes > 0)
            {
                _build.PayloadParts.Add(new PayloadPart(opsPayload, opsLen));
                _build.PayloadBytes = newSize;
            }
            else
            {
                // If it's an empty part but pooled, return it immediately
                if (!ReferenceEquals(opsPayload, Array.Empty<byte>()))
                    ArrayPool<byte>.Shared.Return(opsPayload, clearArray: false);
            }

            _build.OpCount = (ushort)(_build.OpCount + opCount);
        }

        public bool HasBufferedOpsForTick(int serverTick)
        {
            return _build.HasTick && _build.Tick == serverTick && _build.OpCount > 0;
        }

        /// <summary>
        /// Finalizes the coalesced payload into a ServerOps packet and stores it as pending (pooled bytes).
        /// Returns an ArraySegment for immediate send. Segment remains valid until acked (pending window).
        /// Returns default if nothing flushed.
        /// </summary>
        public ArraySegment<byte> FlushToPacketIfAny(int serverTick, uint stateHash)
        {
            if (!_build.HasTick || _build.Tick != serverTick)
                return default;

            if (_build.OpCount == 0)
            {
                _build.ResetAndReturnParts();
                return default;
            }

            // Combine payload parts into a single pooled buffer
            byte[] payload = CombinePayloadPartsPooled(_build.PayloadParts, _build.PayloadBytes, out int payloadLen);

            // Build packet bytes into reusable writer (no body allocation)
            ServerOpsMsg msg = new ServerOpsMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                serverTick,
                NextSeq++,
                stateHash,
                _build.OpCount,
                SliceToExactArray(payload, payloadLen)); // keep payload array exact for MemoryPack field

            ArraySegment<byte> packetSeg = MsgCodecFast.EncodeServerOpsToBuffer(_packetWriter, msg);

            // Copy packet into pooled buffer we can hold until ack (since writer is reused)
            byte[] packetBuf = ArrayPool<byte>.Shared.Rent(packetSeg.Count);
            Buffer.BlockCopy(packetSeg.Array!, packetSeg.Offset, packetBuf, 0, packetSeg.Count);

            uint seq = msg.ServerSeq;
            _pending.Enqueue(new PendingPacket(seq, serverTick, packetBuf, packetSeg.Count));

            while (_pending.Count > _maxPendingPackets)
            {
                PendingPacket old = _pending.Dequeue();
                if (old.PacketBytes != null)
                    ArrayPool<byte>.Shared.Return(old.PacketBytes, clearArray: false);
            }

            // Return op parts buffers and reset
            _build.ResetAndReturnParts();

            // We no longer need the combined payload buffer beyond the msg build step
            // because we copied packet bytes. Return it now.
            if (!ReferenceEquals(payload, Array.Empty<byte>()))
                ArrayPool<byte>.Shared.Return(payload, clearArray: false);

            // Return segment pointing to pooled packetBuf
            return new ArraySegment<byte>(packetBuf, 0, packetSeg.Count);
        }

        /// <summary>
        /// Enumerate all unacked packets (as segments). Do not store these segments; send immediately.
        /// </summary>
        public IEnumerable<ArraySegment<byte>> GetUnackedPacketSegments()
        {
            foreach (PendingPacket p in _pending)
                yield return new ArraySegment<byte>(p.PacketBytes, 0, p.PacketLen);
        }

        private static byte[] CombinePayloadPartsPooled(List<PayloadPart> parts, int totalBytes, out int totalLen)
        {
            totalLen = 0;
            if (totalBytes <= 0 || parts.Count == 0)
                return Array.Empty<byte>();

            if (parts.Count == 1)
            {
                totalLen = parts[0].Len;
                byte[] single = parts[0].Buf;
                parts.Clear();
                return single;
            }

            byte[] dst = ArrayPool<byte>.Shared.Rent(totalBytes);
            int offset = 0;

            for (int i = 0; i < parts.Count; i++)
            {
                PayloadPart p = parts[i];
                Buffer.BlockCopy(p.Buf, 0, dst, offset, p.Len);
                offset += p.Len;

                // return part buffers as we consume them
                ArrayPool<byte>.Shared.Return(p.Buf, clearArray: false);
            }

            parts.Clear();
            totalLen = offset;
            return dst;
        }

        /// <summary>
        /// MemoryPack payload is a byte[] field; it will serialize full array.
        /// So we must provide an exact-length array.
        /// This is the one unavoidable allocation unless you change payload type to Memory<byte>.
        /// For now we keep it minimal and predictable.
        /// </summary>
        private static byte[] SliceToExactArray(byte[] pooled, int len)
        {
            if (len <= 0) return Array.Empty<byte>();
            if (pooled.Length == len) return pooled; // rare but possible
            byte[] exact = new byte[len];
            Buffer.BlockCopy(pooled, 0, exact, 0, len);
            return exact;
        }

        private readonly struct PendingPacket
        {
            public readonly uint Seq;
            public readonly int Tick;
            public readonly byte[] PacketBytes;
            public readonly int PacketLen;

            public PendingPacket(uint seq, int tick, byte[] packetBytes, int packetLen)
            {
                Seq = seq;
                Tick = tick;
                PacketBytes = packetBytes;
                PacketLen = packetLen;
            }
        }

        private readonly struct PayloadPart
        {
            public readonly byte[] Buf;
            public readonly int Len;
            public PayloadPart(byte[] buf, int len) { Buf = buf; Len = len; }
        }

        private sealed class PendingBuild
        {
            public bool HasTick;
            public int Tick;
            public ushort OpCount;

            public List<PayloadPart> PayloadParts;
            public int PayloadBytes;

            public PendingBuild()
            {
                HasTick = false;
                Tick = 0;
                OpCount = 0;
                PayloadParts = new List<PayloadPart>(8);
                PayloadBytes = 0;
            }

            public void ResetAndReturnParts()
            {
                // if parts weren't consumed, return buffers
                for (int i = 0; i < PayloadParts.Count; i++)
                {
                    PayloadPart p = PayloadParts[i];
                    if (p.Buf != null && !ReferenceEquals(p.Buf, Array.Empty<byte>()))
                        ArrayPool<byte>.Shared.Return(p.Buf, clearArray: false);
                }

                HasTick = false;
                Tick = 0;
                OpCount = 0;
                PayloadParts.Clear();
                PayloadBytes = 0;
            }
        }
    }
}
