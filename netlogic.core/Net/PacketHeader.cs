using System;
using System.Buffers.Binary;

namespace com.aqua.netlogic.net
{
    /// <summary>
    /// Fixed header that can be read without deserializing payload.
    /// Packet bytes layout (little-endian):
    /// [ushort Magic][ushort Version][uint BuildHash][byte Kind][byte Flags][ushort PayloadLength]
    /// Total header size = 12 bytes.
    /// </summary>
    public readonly struct PacketHeader
    {
        public const ushort MagicValue = (ushort)0x4C4Eu; // 'N''L' in little-endian

        public const int Size = 12;

        public readonly ushort Magic;
        public readonly ushort Version;
        public readonly uint BuildHash;
        public readonly MsgKind Kind;
        public readonly byte Flags;
        public readonly ushort PayloadLength;

        public PacketHeader(ushort version, uint buildHash, MsgKind kind, byte flags, ushort payloadLength)
        {
            Magic = MagicValue;
            Version = version;
            BuildHash = buildHash;
            Kind = kind;
            Flags = flags;
            PayloadLength = payloadLength;
        }

        public void Write(Span<byte> dst)
        {
            if (dst.Length < Size)
                throw new ArgumentException("dst too small", nameof(dst));

            BinaryPrimitives.WriteUInt16LittleEndian(dst.Slice(0, 2), Magic);
            BinaryPrimitives.WriteUInt16LittleEndian(dst.Slice(2, 2), Version);
            BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(4, 4), BuildHash);

            dst[8] = (byte)Kind;
            dst[9] = Flags;

            BinaryPrimitives.WriteUInt16LittleEndian(dst.Slice(10, 2), PayloadLength);
        }

        public static bool TryRead(ReadOnlySpan<byte> src, out PacketHeader header)
        {
            header = default(PacketHeader);

            if (src.Length < Size)
                return false;

            ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(0, 2));
            if (magic != MagicValue)
                return false;

            ushort version = BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(2, 2));
            uint buildHash = BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(4, 4));

            MsgKind kind = (MsgKind)src[8];
            byte flags = src[9];

            ushort payloadLen = BinaryPrimitives.ReadUInt16LittleEndian(src.Slice(10, 2));

            header = new PacketHeader(version, buildHash, kind, flags, payloadLen);
            return true;
        }
    }
}
