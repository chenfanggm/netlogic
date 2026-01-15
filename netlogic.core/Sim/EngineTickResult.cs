using System;

namespace Sim
{
    /// <summary>
    /// Authoritative engine output for a single fixed server tick.
    /// Contains ONLY engine-level data — no transport, protocol, or lane concepts.
    /// </summary>
    public readonly struct EngineTickResult
    {
        public readonly int ServerTick;
        public readonly long ServerTimeMs;
        public readonly uint WorldHash;

        /// <summary>
        /// Lightweight continuous snapshot (typically encoded into Sample lane).
        /// </summary>
        public readonly SampleEntityPos[] SamplePositions;

        /// <summary>
        /// Discrete reliable ops produced by the engine this tick.
        /// The network adapter decides how to packetize and send these.
        /// </summary>
        public readonly EngineReliableOpBatch[] ReliableOps;

        public EngineTickResult(
            int serverTick,
            long serverTimeMs,
            uint worldHash,
            SampleEntityPos[] samplePositions,
            EngineReliableOpBatch[] reliableOps)
        {
            ServerTick = serverTick;
            ServerTimeMs = serverTimeMs;
            WorldHash = worldHash;
            SamplePositions = samplePositions ?? Array.Empty<SampleEntityPos>();
            ReliableOps = reliableOps ?? Array.Empty<EngineReliableOpBatch>();
        }
    }

    /// <summary>
    /// Minimal continuous state for interpolation.
    /// Protocol encoding is handled by the adapter.
    /// </summary>
    public readonly struct SampleEntityPos
    {
        public readonly int EntityId;
        public readonly int X;
        public readonly int Y;

        public SampleEntityPos(int entityId, int x, int y)
        {
            EntityId = entityId;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Engine-level batch of discrete reliable ops.
    /// Payload is engine-defined; adapter encodes to wire.
    /// </summary>
    public readonly struct EngineReliableOpBatch
    {
        public readonly int ConnId;
        public readonly ushort OpCount;
        public readonly byte[] OpsPayload;

        public EngineReliableOpBatch(int connId, ushort opCount, byte[] opsPayload)
        {
            ConnId = connId;
            OpCount = opCount;
            OpsPayload = opsPayload ?? Array.Empty<byte>();
        }
    }
}
