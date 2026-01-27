using System;
using System.Collections.Generic;
using Net;

namespace Sim.Server.Reliability
{
    /// <summary>
    /// Per-client reliable stream:
    /// - Coalesces ops into a single ServerOpsMsg per tick (or when forced)
    /// - Tracks acked seq and resend window
    /// - Can replay pending (unacked) messages on demand (reconnect/resync)
    /// </summary>
    public sealed class ServerReliableStream
    {
        private readonly int _maxOpsPayloadBytes;
        private readonly int _maxPendingPackets;

        private readonly Queue<PendingPacket> _pending;
        private PendingBuild _build;

        public uint LastAckedSeq { get; private set; }
        public uint NextSeq { get; private set; }

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

            // Drop acked from pending window
            while (_pending.Count > 0 && _pending.Peek().Seq <= LastAckedSeq)
                _pending.Dequeue();
        }

        /// <summary>
        /// Adds an already-encoded op blob into this tick's coalesced buffer.
        /// You build ops with OpsWriter into NetDataWriter; pass opsBytes and opCount.
        /// </summary>
        public void AddOpsForTick(int serverTick, ushort opCount, byte[] opsPayload)
        {
            if (opsPayload == null)
                opsPayload = Array.Empty<byte>();

            if (_build.HasTick && _build.Tick != serverTick)
                throw new InvalidOperationException("AddOpsForTick called with different tick without flushing.");

            if (!_build.HasTick)
            {
                _build.HasTick = true;
                _build.Tick = serverTick;
            }

            // Coalesce: append ops payload; keep count
            int newSize = _build.PayloadBytes + opsPayload.Length;
            if (newSize > _maxOpsPayloadBytes)
            {
                // If would exceed, caller should flush before adding more ops.
                // We throw to make this deterministic and obvious.
                throw new InvalidOperationException("Reliable ops payload exceeded max bytes. Flush before adding more ops.");
            }

            if (opsPayload.Length > 0)
            {
                _build.PayloadParts.Add(opsPayload);
                _build.PayloadBytes = newSize;
            }

            _build.OpCount = (ushort)(_build.OpCount + opCount);
        }

        public bool HasBufferedOpsForTick(int serverTick)
        {
            return _build.HasTick && _build.Tick == serverTick && _build.OpCount > 0;
        }

        /// <summary>
        /// Finalizes the coalesced payload into a ServerOpsMsg packet (encoded bytes) and stores it as pending.
        /// Returns null if there is nothing to flush.
        /// </summary>
        public byte[] FlushToPacketIfAny(int serverTick, uint stateHash)
        {
            if (!_build.HasTick || _build.Tick != serverTick)
                return null!;

            if (_build.OpCount == 0)
            {
                _build.Reset();
                return null!;
            }

            byte[] payload = CombinePayloadParts(_build.PayloadParts, _build.PayloadBytes);

            ServerOpsMsg msg = new ServerOpsMsg(
                ProtocolVersion.Current,
                serverTick,
                NextSeq++,
                stateHash,
                _build.OpCount,
                payload);

            byte[] packetBytes = MsgCodec.EncodeServerOps(Lane.Reliable, msg);

            _pending.Enqueue(new PendingPacket(msg.ServerSeq, serverTick, packetBytes));

            while (_pending.Count > _maxPendingPackets)
            {
                // Drop oldest (in real production you might force resync instead)
                _pending.Dequeue();
            }

            _build.Reset();

            return packetBytes;
        }

        /// <summary>
        /// Returns all unacked packets for resend/replay.
        /// </summary>
        public IEnumerable<byte[]> GetUnackedPackets()
        {
            foreach (PendingPacket p in _pending)
                yield return p.PacketBytes;
        }

        private static byte[] CombinePayloadParts(List<byte[]> parts, int totalBytes)
        {
            if (totalBytes <= 0 || parts.Count == 0)
                return Array.Empty<byte>();

            if (parts.Count == 1)
                return parts[0];

            byte[] dst = new byte[totalBytes];
            int offset = 0;

            int i = 0;
            while (i < parts.Count)
            {
                byte[] src = parts[i];
                Buffer.BlockCopy(src, 0, dst, offset, src.Length);
                offset += src.Length;
                i++;
            }

            return dst;
        }

        private readonly struct PendingPacket
        {
            public readonly uint Seq;
            public readonly int Tick;
            public readonly byte[] PacketBytes;

            public PendingPacket(uint seq, int tick, byte[] packetBytes)
            {
                Seq = seq;
                Tick = tick;
                PacketBytes = packetBytes;
            }
        }

        private sealed class PendingBuild
        {
            public bool HasTick;
            public int Tick;

            public ushort OpCount;

            public List<byte[]> PayloadParts;
            public int PayloadBytes;

            public PendingBuild()
            {
                HasTick = false;
                Tick = 0;
                OpCount = 0;
                PayloadParts = new List<byte[]>(8);
                PayloadBytes = 0;
            }

            public void Reset()
            {
                HasTick = false;
                Tick = 0;
                OpCount = 0;
                PayloadParts.Clear();
                PayloadBytes = 0;
            }
        }
    }
}
