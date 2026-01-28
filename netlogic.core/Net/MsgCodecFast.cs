using System;
using System.Buffers;
using System.Runtime.InteropServices;
using MemoryPack;

namespace com.aqua.netlogic.net
{
    /// <summary>
    /// Fast-path encode helpers that avoid allocating:
    /// - no intermediate "body byte[]"
    /// - reuse a caller-owned ArrayBufferWriter
    ///
    /// Still produces a contiguous packet buffer (header + payload) like MsgCodec.EncodePayload.
    /// </summary>
    internal static class MsgCodecFast
    {
        public static ArraySegment<byte> EncodeToBuffer<T>(
            ArrayBufferWriter<byte> w,
            MsgKind kind,
            byte flags,
            T payload)
        {
            w.Clear();

            // Reserve header space
            w.GetSpan(PacketHeader.Size).Slice(0, PacketHeader.Size).Clear();
            w.Advance(PacketHeader.Size);

            int bodyStart = w.WrittenCount;

            // Serialize payload directly into the same buffer (no body allocation)
            MemoryPackSerializer.Serialize(w, payload);

            int bodyLen = w.WrittenCount - bodyStart;
            if (bodyLen > MsgCodec.MaxPayloadBytes)
                throw new InvalidOperationException("Payload too large: " + bodyLen);

            if (bodyLen > ushort.MaxValue)
                throw new InvalidOperationException("Payload too large for ushort length: " + bodyLen);

            ushort payloadLen = (ushort)bodyLen;

            PacketHeader header = new PacketHeader(
                version: Protocol.Version,
                buildHash: Protocol.BuildHash,
                kind: kind,
                flags: flags,
                payloadLength: payloadLen);

            // Write header into the first bytes
            if (!MemoryMarshal.TryGetArray(w.WrittenMemory, out ArraySegment<byte> headerSeg))
                throw new InvalidOperationException("ArrayBufferWriter is not array-backed (unexpected).");

            header.Write(headerSeg.Array.AsSpan(headerSeg.Offset, PacketHeader.Size));

            // Expose packet as ArraySegment<byte> for transport.Send
            return ToArraySegment(w);
        }

        public static ArraySegment<byte> EncodeServerOpsToBuffer(ArrayBufferWriter<byte> w, ServerOpsMsg msg)
            => EncodeToBuffer(w, MsgKind.ServerOps, PacketFlags.None, msg);

        public static ArraySegment<byte> EncodePongToBuffer(ArrayBufferWriter<byte> w, PongMsg msg)
            => EncodeToBuffer(w, MsgKind.Pong, PacketFlags.None, msg);

        public static ArraySegment<byte> EncodeWelcomeToBuffer(ArrayBufferWriter<byte> w, Welcome msg)
            => EncodeToBuffer(w, MsgKind.Welcome, PacketFlags.None, msg);

        public static ArraySegment<byte> EncodeBaselineToBuffer(ArrayBufferWriter<byte> w, BaselineMsg msg)
            => EncodeToBuffer(w, MsgKind.Baseline, PacketFlags.None, msg);

        private static ArraySegment<byte> ToArraySegment(ArrayBufferWriter<byte> w)
        {
            if (!MemoryMarshal.TryGetArray(w.WrittenMemory, out ArraySegment<byte> seg))
                throw new InvalidOperationException("ArrayBufferWriter is not array-backed (unexpected).");

            return seg;
        }
    }
}
