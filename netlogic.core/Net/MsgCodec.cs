using System;

namespace Net
{
    public enum MsgKind : byte
    {
        ClientOps = 1,
        ServerOps = 2
    }

    public sealed class ClientOpsMsg
    {
        public int Tick;
        public ushort OpCount;
        public ArraySegment<byte> OpsPayload;

        public ClientOpsMsg(int tick, ushort opCount, ArraySegment<byte> opsPayload)
        {
            Tick = tick;
            OpCount = opCount;
            OpsPayload = opsPayload;
        }
    }

    public sealed class ServerOpsMsg
    {
        public int Tick;
        public Lane Lane;
        public ushort OpCount;
        public ArraySegment<byte> OpsPayload;

        public ServerOpsMsg(int tick, Lane lane, ushort opCount, ArraySegment<byte> opsPayload)
        {
            Tick = tick;
            Lane = lane;
            OpCount = opCount;
            OpsPayload = opsPayload;
        }
    }

    public static class MsgCodec
    {
        public static byte[] EncodeClientOps(ClientOpsMsg msg)
        {
            int headerSize = 1 + 4 + 2;
            int payloadSize = msg.OpsPayload.Count;
            byte[] bytes = new byte[headerSize + payloadSize];

            int o = 0;
            bytes[o++] = (byte)MsgKind.ClientOps;

            WriteInt(bytes, ref o, msg.Tick);
            WriteUShort(bytes, ref o, msg.OpCount);

            Buffer.BlockCopy(msg.OpsPayload.Array!, msg.OpsPayload.Offset, bytes, o, payloadSize);
            return bytes;
        }

        public static byte[] EncodeServerOps(ServerOpsMsg msg)
        {
            int headerSize = 1 + 1 + 4 + 2;
            int payloadSize = msg.OpsPayload.Count;
            byte[] bytes = new byte[headerSize + payloadSize];

            int o = 0;
            bytes[o++] = (byte)MsgKind.ServerOps;
            bytes[o++] = (byte)msg.Lane;

            WriteInt(bytes, ref o, msg.Tick);
            WriteUShort(bytes, ref o, msg.OpCount);

            Buffer.BlockCopy(msg.OpsPayload.Array!, msg.OpsPayload.Offset, bytes, o, payloadSize);
            return bytes;
        }

        public static bool TryDecodeClientOps(ArraySegment<byte> bytes, out ClientOpsMsg msg)
        {
            msg = null!;

            if (bytes.Count < 1 + 4 + 2)
                return false;

            int o = bytes.Offset;
            byte kind = bytes.Array![o++];

            if (kind != (byte)MsgKind.ClientOps)
                return false;

            int tick = ReadInt(bytes.Array, ref o);
            ushort opCount = ReadUShort(bytes.Array, ref o);

            int payloadOffset = o;
            int payloadCount = (bytes.Offset + bytes.Count) - payloadOffset;

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes.Array, payloadOffset, payloadCount);
            msg = new ClientOpsMsg(tick, opCount, payload);
            return true;
        }

        public static bool TryDecodeServerOps(ArraySegment<byte> bytes, out ServerOpsMsg msg)
        {
            msg = null!;

            if (bytes.Count < 1 + 1 + 4 + 2)
                return false;

            int o = bytes.Offset;
            byte kind = bytes.Array![o++];

            if (kind != (byte)MsgKind.ServerOps)
                return false;

            Lane lane = (Lane)bytes.Array[o++];

            int tick = ReadInt(bytes.Array, ref o);
            ushort opCount = ReadUShort(bytes.Array, ref o);

            int payloadOffset = o;
            int payloadCount = (bytes.Offset + bytes.Count) - payloadOffset;

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes.Array, payloadOffset, payloadCount);
            msg = new ServerOpsMsg(tick, lane, opCount, payload);
            return true;
        }

        private static void WriteInt(byte[] b, ref int o, int v)
        {
            b[o++] = (byte)(v);
            b[o++] = (byte)(v >> 8);
            b[o++] = (byte)(v >> 16);
            b[o++] = (byte)(v >> 24);
        }

        private static int ReadInt(byte[] b, ref int o)
        {
            int v0 = b[o++];
            int v1 = b[o++] << 8;
            int v2 = b[o++] << 16;
            int v3 = b[o++] << 24;
            return v0 | v1 | v2 | v3;
        }

        private static void WriteUShort(byte[] b, ref int o, ushort v)
        {
            b[o++] = (byte)(v);
            b[o++] = (byte)(v >> 8);
        }

        private static ushort ReadUShort(byte[] b, ref int o)
        {
            int v0 = b[o++];
            int v1 = b[o++] << 8;
            return (ushort)(v0 | v1);
        }
    }
}

