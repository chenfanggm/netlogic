using System;
using LiteNetLib.Utils;

namespace Net
{
    public sealed class Hello
    {
        public readonly ushort Version;
        public readonly uint BuildHash;
        public readonly int ClientTickRateHz;

        public Hello(ushort version, uint buildHash, int clientTickRateHz)
        {
            Version = version;
            BuildHash = buildHash;
            ClientTickRateHz = clientTickRateHz;
        }
    }

    public sealed class Welcome
    {
        public readonly ushort Version;
        public readonly uint BuildHash;
        public readonly int ServerTickRateHz;
        public readonly int ServerTick;

        public Welcome(ushort version, uint buildHash, int serverTickRateHz, int serverTick)
        {
            Version = version;
            BuildHash = buildHash;
            ServerTickRateHz = serverTickRateHz;
            ServerTick = serverTick;
        }
    }

    public sealed class ClientOpsMsg
    {
        public readonly int ClientTick;
        public readonly uint ClientCmdSeq;
        public readonly ushort OpCount;
        public readonly ArraySegment<byte> OpsPayload;

        public ClientOpsMsg(int clientTick, uint clientCmdSeq, ushort opCount, ArraySegment<byte> opsPayload)
        {
            ClientTick = clientTick;
            ClientCmdSeq = clientCmdSeq;
            OpCount = opCount;
            OpsPayload = opsPayload;
        }
    }

    public sealed class ServerOpsMsg
    {
        public readonly int ServerTick;
        public readonly Lane Lane;
        public readonly uint ServerSeq;
        public readonly uint StateHash;
        public readonly ushort OpCount;
        public readonly ArraySegment<byte> OpsPayload;

        public ServerOpsMsg(int serverTick, Lane lane, uint serverSeq, uint stateHash, ushort opCount, ArraySegment<byte> opsPayload)
        {
            ServerTick = serverTick;
            Lane = lane;
            ServerSeq = serverSeq;
            StateHash = stateHash;
            OpCount = opCount;
            OpsPayload = opsPayload;
        }
    }

    public sealed class BaselineMsg
    {
        public readonly int ServerTick;
        public readonly uint StateHash;
        public readonly EntityState[] Entities;

        public BaselineMsg(int serverTick, uint stateHash, EntityState[] entities)
        {
            ServerTick = serverTick;
            StateHash = stateHash;
            Entities = entities;
        }
    }

    public sealed class PingMsg
    {
        public readonly uint PingId;
        public readonly long ClientTimeMs;
        public readonly int ClientTick;

        public PingMsg(uint pingId, long clientTimeMs, int clientTick)
        {
            PingId = pingId;
            ClientTimeMs = clientTimeMs;
            ClientTick = clientTick;
        }
    }

    public sealed class PongMsg
    {
        public readonly uint PingId;
        public readonly long ClientTimeMsEcho;
        public readonly long ServerTimeMs;
        public readonly int ServerTick;

        public PongMsg(uint pingId, long clientTimeMsEcho, long serverTimeMs, int serverTick)
        {
            PingId = pingId;
            ClientTimeMsEcho = clientTimeMsEcho;
            ServerTimeMs = serverTimeMs;
            ServerTick = serverTick;
        }
    }

    public static class MsgCodec
    {
        // Header for all messages:
        // [byte kind][ushort version][uint buildHash] + message-specific fields
        public static byte[] EncodeHello(int clientTickRateHz)
        {
            NetDataWriter w = new NetDataWriter();
            w.Put((byte)MsgKind.Hello);
            w.Put(Protocol.Version);
            w.Put(Protocol.BuildHash);
            w.Put(clientTickRateHz);
            return w.CopyData();
        }

        public static bool TryDecodeHello(ArraySegment<byte> bytes, out Hello msg)
        {
            msg = null!;
            NetDataReader r = new NetDataReader(bytes.Array, bytes.Offset, bytes.Count);

            if (r.AvailableBytes < 1 + 2 + 4 + 4)
                return false;

            MsgKind k = (MsgKind)r.GetByte();
            if (k != MsgKind.Hello)
                return false;

            ushort ver = r.GetUShort();
            uint hash = r.GetUInt();
            int hz = r.GetInt();

            msg = new Hello(ver, hash, hz);
            return true;
        }

        public static byte[] EncodeWelcome(int serverTickRateHz, int serverTick)
        {
            NetDataWriter w = new NetDataWriter();
            w.Put((byte)MsgKind.Welcome);
            w.Put(Protocol.Version);
            w.Put(Protocol.BuildHash);
            w.Put(serverTickRateHz);
            w.Put(serverTick);
            return w.CopyData();
        }

        public static bool TryDecodeWelcome(ArraySegment<byte> bytes, out Welcome msg)
        {
            msg = null!;
            NetDataReader r = new NetDataReader(bytes.Array, bytes.Offset, bytes.Count);

            if (r.AvailableBytes < 1 + 2 + 4 + 4 + 4)
                return false;

            MsgKind k = (MsgKind)r.GetByte();
            if (k != MsgKind.Welcome)
                return false;

            ushort ver = r.GetUShort();
            uint hash = r.GetUInt();
            int hz = r.GetInt();
            int tick = r.GetInt();

            msg = new Welcome(ver, hash, hz, tick);
            return true;
        }

        public static byte[] EncodeClientOps(ClientOpsMsg msg)
        {
            NetDataWriter w = new NetDataWriter();
            w.Put((byte)MsgKind.ClientOps);
            w.Put(Protocol.Version);
            w.Put(Protocol.BuildHash);

            w.Put(msg.ClientTick);
            w.Put(msg.ClientCmdSeq);
            w.Put(msg.OpCount);

            w.Put(msg.OpsPayload.Array, msg.OpsPayload.Offset, msg.OpsPayload.Count);
            return w.CopyData();
        }

        public static bool TryDecodeClientOps(ArraySegment<byte> bytes, out ClientOpsMsg msg)
        {
            msg = null!;
            NetDataReader r = new NetDataReader(bytes.Array, bytes.Offset, bytes.Count);

            if (r.AvailableBytes < 1 + 2 + 4 + 4 + 4 + 2)
                return false;

            MsgKind k = (MsgKind)r.GetByte();
            if (k != MsgKind.ClientOps)
                return false;

            ushort ver = r.GetUShort();
            uint hash = r.GetUInt();
            if (ver != Protocol.Version)
                return false;
            if (hash != Protocol.BuildHash)
                return false;

            int clientTick = r.GetInt();
            uint cmdSeq = r.GetUInt();
            ushort opCount = r.GetUShort();

            int payloadLen = r.AvailableBytes;
            int payloadOffset = (bytes.Offset + bytes.Count) - payloadLen;

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes.Array!, payloadOffset, payloadLen);

            msg = new ClientOpsMsg(clientTick, cmdSeq, opCount, payload);
            return true;
        }

        public static byte[] EncodeServerOps(ServerOpsMsg msg)
        {
            NetDataWriter w = new NetDataWriter();
            w.Put((byte)MsgKind.ServerOps);
            w.Put(Protocol.Version);
            w.Put(Protocol.BuildHash);

            w.Put(msg.ServerTick);
            w.Put((byte)msg.Lane);
            w.Put(msg.ServerSeq);
            w.Put(msg.StateHash);
            w.Put(msg.OpCount);

            w.Put(msg.OpsPayload.Array, msg.OpsPayload.Offset, msg.OpsPayload.Count);
            return w.CopyData();
        }

        public static bool TryDecodeServerOps(ArraySegment<byte> bytes, out ServerOpsMsg msg)
        {
            msg = null!;
            NetDataReader r = new NetDataReader(bytes.Array, bytes.Offset, bytes.Count);

            if (r.AvailableBytes < 1 + 2 + 4 + 4 + 1 + 4 + 4 + 2)
                return false;

            MsgKind k = (MsgKind)r.GetByte();
            if (k != MsgKind.ServerOps)
                return false;

            ushort ver = r.GetUShort();
            uint hash = r.GetUInt();
            if (ver != Protocol.Version)
                return false;
            if (hash != Protocol.BuildHash)
                return false;

            int serverTick = r.GetInt();
            Lane lane = (Lane)r.GetByte();
            uint serverSeq = r.GetUInt();
            uint stateHash = r.GetUInt();
            ushort opCount = r.GetUShort();

            int payloadLen = r.AvailableBytes;
            int payloadOffset = (bytes.Offset + bytes.Count) - payloadLen;

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes.Array!, payloadOffset, payloadLen);

            msg = new ServerOpsMsg(serverTick, lane, serverSeq, stateHash, opCount, payload);
            return true;
        }

        public static byte[] EncodeBaseline(BaselineMsg msg)
        {
            NetDataWriter w = new NetDataWriter();
            w.Put((byte)MsgKind.Baseline);
            w.Put(Protocol.Version);
            w.Put(Protocol.BuildHash);

            w.Put(msg.ServerTick);
            w.Put(msg.StateHash);

            int count = msg.Entities.Length;
            w.Put(count);

            int i = 0;
            while (i < count)
            {
                EntityState e = msg.Entities[i];
                w.Put(e.Id);
                w.Put(e.X);
                w.Put(e.Y);
                w.Put(e.Hp);
                i++;
            }

            return w.CopyData();
        }

        public static bool TryDecodeBaseline(ArraySegment<byte> bytes, out BaselineMsg msg)
        {
            msg = null!;
            NetDataReader r = new NetDataReader(bytes.Array, bytes.Offset, bytes.Count);

            if (r.AvailableBytes < 1 + 2 + 4 + 4 + 4 + 4)
                return false;

            MsgKind k = (MsgKind)r.GetByte();
            if (k != MsgKind.Baseline)
                return false;

            ushort ver = r.GetUShort();
            uint hash = r.GetUInt();
            if (ver != Protocol.Version)
                return false;
            if (hash != Protocol.BuildHash)
                return false;

            int serverTick = r.GetInt();
            uint stateHash = r.GetUInt();

            int count = r.GetInt();
            if (count < 0)
                return false;

            EntityState[] entities = new EntityState[count];

            int i = 0;
            while (i < count)
            {
                int id = r.GetInt();
                int x = r.GetInt();
                int y = r.GetInt();
                int hp = r.GetInt();
                entities[i] = new EntityState(id, x, y, hp);
                i++;
            }

            msg = new BaselineMsg(serverTick, stateHash, entities);
            return true;
        }

        public static byte[] EncodePing(PingMsg msg)
        {
            NetDataWriter w = new NetDataWriter();
            w.Put((byte)MsgKind.Ping);
            w.Put(Protocol.Version);
            w.Put(Protocol.BuildHash);

            w.Put(msg.PingId);
            w.Put(msg.ClientTimeMs);
            w.Put(msg.ClientTick);

            return w.CopyData();
        }

        public static bool TryDecodePing(ArraySegment<byte> bytes, out PingMsg msg)
        {
            msg = null!;
            NetDataReader r = new NetDataReader(bytes.Array, bytes.Offset, bytes.Count);

            if (r.AvailableBytes < 1 + 2 + 4 + 4 + 8 + 4)
                return false;

            MsgKind k = (MsgKind)r.GetByte();
            if (k != MsgKind.Ping)
                return false;

            ushort ver = r.GetUShort();
            uint hash = r.GetUInt();
            if (ver != Protocol.Version)
                return false;
            if (hash != Protocol.BuildHash)
                return false;

            uint pingId = r.GetUInt();
            long clientMs = r.GetLong();
            int clientTick = r.GetInt();

            msg = new PingMsg(pingId, clientMs, clientTick);
            return true;
        }

        public static byte[] EncodePong(PongMsg msg)
        {
            NetDataWriter w = new NetDataWriter();
            w.Put((byte)MsgKind.Pong);
            w.Put(Protocol.Version);
            w.Put(Protocol.BuildHash);

            w.Put(msg.PingId);
            w.Put(msg.ClientTimeMsEcho);
            w.Put(msg.ServerTimeMs);
            w.Put(msg.ServerTick);

            return w.CopyData();
        }

        public static bool TryDecodePong(ArraySegment<byte> bytes, out PongMsg msg)
        {
            msg = null!;
            NetDataReader r = new NetDataReader(bytes.Array, bytes.Offset, bytes.Count);

            if (r.AvailableBytes < 1 + 2 + 4 + 4 + 8 + 8 + 4)
                return false;

            MsgKind k = (MsgKind)r.GetByte();
            if (k != MsgKind.Pong)
                return false;

            ushort ver = r.GetUShort();
            uint hash = r.GetUInt();
            if (ver != Protocol.Version)
                return false;
            if (hash != Protocol.BuildHash)
                return false;

            uint pingId = r.GetUInt();
            long echo = r.GetLong();
            long serverMs = r.GetLong();
            int serverTick = r.GetInt();

            msg = new PongMsg(pingId, echo, serverMs, serverTick);
            return true;
        }
    }
}
