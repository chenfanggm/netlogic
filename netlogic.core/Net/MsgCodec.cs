using System;
using MemoryPack;
using com.aqua.netlogic.net.wirestate;

namespace com.aqua.netlogic.net
{
    // Flags reserved for future:
    // bit0: compressed
    // bit1: encrypted
    // bit2: fragmented
    public static class PacketFlags
    {
        public const byte None = 0;
        public const byte Compressed = 1 << 0;
        public const byte Encrypted = 1 << 1;
        public const byte Fragmented = 1 << 2;
    }

    // -------------------------
    // MemoryPack payload types
    // -------------------------

    [MemoryPackable]
    public sealed partial class Hello
    {
        public ushort ProtocolVersion;
        public int ClientTickRateHz;

        public Hello()
        {
            ProtocolVersion = com.aqua.netlogic.net.ProtocolVersion.Current;
            ClientTickRateHz = 0;
        }

        [MemoryPackConstructor]
        public Hello(ushort protocolVersion, int clientTickRateHz)
        {
            ProtocolVersion = protocolVersion;
            ClientTickRateHz = clientTickRateHz;
        }
    }

    [MemoryPackable]
    public sealed partial class Welcome
    {
        public ushort ProtocolVersion;
        public int ServerTickRateHz;
        public int ServerTick;
        public double ServerTimeMs;

        public Welcome()
        {
            ProtocolVersion = com.aqua.netlogic.net.ProtocolVersion.Current;
            ServerTickRateHz = 0;
            ServerTick = 0;
            ServerTimeMs = 0;
        }

        [MemoryPackConstructor]
        public Welcome(ushort protocolVersion, int serverTickRateHz, int serverTick, double serverTimeMs)
        {
            ProtocolVersion = protocolVersion;
            ServerTickRateHz = serverTickRateHz;
            ServerTick = serverTick;
            ServerTimeMs = serverTimeMs;
        }
    }

    [MemoryPackable]
    public sealed partial class ClientOpsMsg
    {
        public int ClientTick;
        public uint ClientCmdSeq;
        public ushort OpCount;
        public byte[] OpsPayload;

        public ClientOpsMsg()
        {
            ClientTick = 0;
            ClientCmdSeq = 0;
            OpCount = 0;
            OpsPayload = Array.Empty<byte>();
        }

        [MemoryPackConstructor]
        public ClientOpsMsg(int clientTick, uint clientCmdSeq, ushort opCount, byte[] opsPayload)
        {
            ClientTick = clientTick;
            ClientCmdSeq = clientCmdSeq;
            OpCount = opCount;
            OpsPayload = opsPayload ?? Array.Empty<byte>();
        }
    }

    [MemoryPackable]
    public sealed partial class ServerOpsMsg
    {
        public ushort ProtocolVersion;
        public ushort HashScopeId;
        public byte HashPhase;
        public int ServerTick;
        public uint ServerSeq;
        public uint StateHash;
        public ushort OpCount;
        public byte[] OpsPayload;

        public ServerOpsMsg()
        {
            ProtocolVersion = com.aqua.netlogic.net.ProtocolVersion.Current;
            HashScopeId = com.aqua.netlogic.net.HashContract.ScopeId;
            HashPhase = (byte)com.aqua.netlogic.net.HashContract.Phase;
            ServerTick = 0;
            ServerSeq = 0;
            StateHash = 0;
            OpCount = 0;
            OpsPayload = Array.Empty<byte>();
        }

        [MemoryPackConstructor]
        public ServerOpsMsg(
            ushort protocolVersion,
            ushort hashScopeId,
            byte hashPhase,
            int serverTick,
            uint serverSeq,
            uint stateHash,
            ushort opCount,
            byte[] opsPayload)
        {
            ProtocolVersion = protocolVersion;
            HashScopeId = hashScopeId;
            HashPhase = hashPhase;
            ServerTick = serverTick;
            ServerSeq = serverSeq;
            StateHash = stateHash;
            OpCount = opCount;
            OpsPayload = opsPayload ?? Array.Empty<byte>();
        }
    }

    [MemoryPackable]
    public sealed partial class BaselineMsg
    {
        public ushort ProtocolVersion;
        public ushort HashScopeId;
        public byte HashPhase;
        public ushort BaselineSchemaId;
        public int ServerTick;
        public uint StateHash;
        public WireFlowState Flow;
        public WireEntityState[] Entities;

        public BaselineMsg()
        {
            ProtocolVersion = com.aqua.netlogic.net.ProtocolVersion.Current;
            HashScopeId = com.aqua.netlogic.net.HashContract.ScopeId;
            HashPhase = (byte)com.aqua.netlogic.net.HashContract.Phase;
            BaselineSchemaId = com.aqua.netlogic.net.StateSchema.BaselineSchemaId;
            ServerTick = 0;
            StateHash = 0;
            Flow = default;
            Entities = Array.Empty<WireEntityState>();
        }

        [MemoryPackConstructor]
        public BaselineMsg(
            ushort protocolVersion,
            ushort hashScopeId,
            byte hashPhase,
            ushort baselineSchemaId,
            int serverTick,
            uint stateHash,
            WireFlowState flow,
            WireEntityState[] entities)
        {
            ProtocolVersion = protocolVersion;
            HashScopeId = hashScopeId;
            HashPhase = hashPhase;
            BaselineSchemaId = baselineSchemaId;
            ServerTick = serverTick;
            StateHash = stateHash;
            Flow = flow;
            Entities = entities ?? Array.Empty<WireEntityState>();
        }
    }

    [MemoryPackable]
    public sealed partial class PingMsg
    {
        public uint PingId;
        public long ClientTimeMs;
        public int ClientTick;

        public PingMsg()
        {
            PingId = 0;
            ClientTimeMs = 0;
            ClientTick = 0;
        }

        [MemoryPackConstructor]
        public PingMsg(uint pingId, long clientTimeMs, int clientTick)
        {
            PingId = pingId;
            ClientTimeMs = clientTimeMs;
            ClientTick = clientTick;
        }
    }

    [MemoryPackable]
    public sealed partial class PongMsg
    {
        public uint PingId;
        public long ClientTimeMsEcho;
        public double ServerTimeMs;
        public int ServerTick;

        public PongMsg()
        {
            PingId = 0;
            ClientTimeMsEcho = 0;
            ServerTimeMs = 0;
            ServerTick = 0;
        }

        [MemoryPackConstructor]
        public PongMsg(uint pingId, long clientTimeMsEcho, double serverTimeMs, int serverTick)
        {
            PingId = pingId;
            ClientTimeMsEcho = clientTimeMsEcho;
            ServerTimeMs = serverTimeMs;
            ServerTick = serverTick;
        }
    }

    [MemoryPackable]
    public sealed partial class ClientAckMsg
    {
        public uint LastAckedReliableSeq;

        public ClientAckMsg()
        {
            LastAckedReliableSeq = 0;
        }

        [MemoryPackConstructor]
        public ClientAckMsg(uint lastAckedReliableSeq)
        {
            LastAckedReliableSeq = lastAckedReliableSeq;
        }
    }

    // -------------------------
    // Codec (header → route → payload)
    // -------------------------

    public static class MsgCodec
    {
        // Safety cap (tune for your game)
        public const int MaxPayloadBytes = 60 * 1024;

        public static byte[] EncodeHello(int clientTickRateHz)
        {
            Hello payload = new Hello(ProtocolVersion.Current, clientTickRateHz);
            return EncodePayload(MsgKind.Hello, PacketFlags.None, payload);
        }

        public static bool TryDecodeHello(ArraySegment<byte> packetBytes, out Hello msg)
        {
            msg = null!;
            return TryDecodePayload(packetBytes, MsgKind.Hello, out msg);
        }

        public static byte[] EncodeWelcome(int serverTickRateHz, int serverTick, double serverTimeMs)
        {
            Welcome payload = new Welcome(ProtocolVersion.Current, serverTickRateHz, serverTick, serverTimeMs);
            return EncodePayload(MsgKind.Welcome, PacketFlags.None, payload);
        }

        public static bool TryDecodeWelcome(ArraySegment<byte> packetBytes, out Welcome msg)
        {
            msg = null!;
            if (!TryDecodePayload(packetBytes, MsgKind.Welcome, out msg))
                return false;

            if (msg.ProtocolVersion != ProtocolVersion.Current)
            {
                Console.WriteLine(
                    $"[Net] Protocol mismatch on Welcome. Client={ProtocolVersion.Current} Server={msg.ProtocolVersion}");
                msg = null!;
                return false;
            }

            return true;
        }

        public static byte[] EncodeClientOps(ClientOpsMsg msg)
        {
            return EncodePayload(MsgKind.ClientOps, PacketFlags.None, msg);
        }

        public static bool TryDecodeClientOps(ArraySegment<byte> packetBytes, out ClientOpsMsg msg)
        {
            msg = null!;
            return TryDecodePayload(packetBytes, MsgKind.ClientOps, out msg);
        }

        public static byte[] EncodeServerOps(Lane lane, ServerOpsMsg msg)
        {
            // Lane is not part of the payload; you already have lane on transport.
            // We keep it out of payload so routing stays fast.
            // Just call transport.Send(lane, packetBytes).
            return EncodePayload(MsgKind.ServerOps, PacketFlags.None, msg);
        }

        public static bool TryDecodeServerOps(ArraySegment<byte> packetBytes, out ServerOpsMsg msg)
        {
            msg = null!;
            if (!TryDecodePayload(packetBytes, MsgKind.ServerOps, out msg))
                return false;

            if (msg.ProtocolVersion != ProtocolVersion.Current)
            {
                Console.WriteLine(
                    $"[Net] Protocol mismatch on ServerOps. Client={ProtocolVersion.Current} Server={msg.ProtocolVersion}");
                msg = null!;
                return false;
            }

            if (msg.HashScopeId != HashContract.ScopeId || msg.HashPhase != (byte)HashContract.Phase)
            {
                Console.WriteLine(
                    $"[Net] Hash contract mismatch on ServerOps. Client scope/phase={HashContract.ScopeId}/{(byte)HashContract.Phase} " +
                    $"Server scope/phase={msg.HashScopeId}/{msg.HashPhase}");
                msg = null!;
                return false;
            }

            return true;
        }

        public static byte[] EncodeBaseline(BaselineMsg msg)
        {
            return EncodePayload(MsgKind.Baseline, PacketFlags.None, msg);
        }

        public static bool TryDecodeBaseline(ArraySegment<byte> packetBytes, out BaselineMsg msg)
        {
            msg = null!;
            if (!TryDecodePayload(packetBytes, MsgKind.Baseline, out msg))
                return false;

            if (msg.ProtocolVersion != ProtocolVersion.Current)
            {
                Console.WriteLine(
                    $"[Net] Protocol mismatch on Baseline. Client={ProtocolVersion.Current} Server={msg.ProtocolVersion}");
                msg = null!;
                return false;
            }

            if (msg.HashScopeId != HashContract.ScopeId || msg.HashPhase != (byte)HashContract.Phase)
            {
                Console.WriteLine(
                    $"[Net] Hash contract mismatch on Baseline. Client scope/phase={HashContract.ScopeId}/{(byte)HashContract.Phase} " +
                    $"Server scope/phase={msg.HashScopeId}/{msg.HashPhase}");
                msg = null!;
                return false;
            }

            if (msg.BaselineSchemaId != StateSchema.BaselineSchemaId)
            {
                Console.WriteLine(
                    $"[Net] Baseline schema mismatch. Client={StateSchema.BaselineSchemaId} Server={msg.BaselineSchemaId}");
                msg = null!;
                return false;
            }

            return true;
        }

        public static byte[] EncodePing(PingMsg msg)
        {
            return EncodePayload(MsgKind.Ping, PacketFlags.None, msg);
        }

        public static bool TryDecodePing(ArraySegment<byte> packetBytes, out PingMsg msg)
        {
            msg = null!;
            return TryDecodePayload(packetBytes, MsgKind.Ping, out msg);
        }

        public static byte[] EncodePong(PongMsg msg)
        {
            return EncodePayload(MsgKind.Pong, PacketFlags.None, msg);
        }

        public static bool TryDecodePong(ArraySegment<byte> packetBytes, out PongMsg msg)
        {
            msg = null!;
            return TryDecodePayload(packetBytes, MsgKind.Pong, out msg);
        }

        public static byte[] EncodeClientAck(ClientAckMsg msg)
        {
            return EncodePayload(MsgKind.ClientAck, PacketFlags.None, msg);
        }

        public static bool TryDecodeClientAck(ArraySegment<byte> packetBytes, out ClientAckMsg msg)
        {
            msg = null!;
            return TryDecodePayload(packetBytes, MsgKind.ClientAck, out msg);
        }

        // -------------------------
        // Core encode/decode helpers
        // -------------------------

        private static byte[] EncodePayload<T>(MsgKind kind, byte flags, T payload)
        {
            // Serialize with MemoryPack
            byte[] body = MemoryPackSerializer.Serialize(payload);

            if (body.Length > MaxPayloadBytes)
                throw new InvalidOperationException("Payload too large: " + body.Length);

            if (body.Length > ushort.MaxValue)
                throw new InvalidOperationException("Payload too large for ushort length: " + body.Length);

            ushort payloadLen = (ushort)body.Length;

            PacketHeader header = new PacketHeader(
                version: Protocol.Version,
                buildHash: Protocol.BuildHash,
                kind: kind,
                flags: flags,
                payloadLength: payloadLen);

            byte[] packet = new byte[PacketHeader.Size + payloadLen];

            // header
            header.Write(packet.AsSpan(0, PacketHeader.Size));

            // body
            Buffer.BlockCopy(body, 0, packet, PacketHeader.Size, payloadLen);

            return packet;
        }

        private static bool TryDecodePayload<T>(ArraySegment<byte> packetBytes, MsgKind expectedKind, out T payload)
        {
            payload = default(T)!;

            if (packetBytes.Array == null)
                return false;

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(packetBytes.Array, packetBytes.Offset, packetBytes.Count);

            PacketHeader header;
            bool ok = PacketHeader.TryRead(span, out header);
            if (!ok)
                return false;

            if (header.Kind != expectedKind)
                return false;

            if (header.Version != Protocol.Version)
                return false;

            if (header.BuildHash != Protocol.BuildHash)
                return false;

            int totalNeeded = PacketHeader.Size + header.PayloadLength;
            if (span.Length < totalNeeded)
                return false;

            if (header.PayloadLength > MaxPayloadBytes)
                return false;

            ReadOnlySpan<byte> body = span.Slice(PacketHeader.Size, header.PayloadLength);

            // Deserialize MemoryPack payload
            T? result = MemoryPackSerializer.Deserialize<T>(body);
            if (result == null)
                return false;

            payload = result;
            return true;
        }
    }
}
