using Sim.Snapshot;

namespace Sim.Engine
{
    /// <summary>
    /// Authoritative engine output for a single fixed server tick.
    /// NOTE: No protocol, no transport, no hashing, no lanes.
    /// </summary>
    public readonly struct EngineTickResult
    {
        public readonly int ServerTick;
        public readonly double ServerTimeMs;

        /// <summary>
        /// Continuous snapshot for rendering/interpolation.
        /// Adapter typically encodes this into Sample lane (latest-wins).
        /// </summary>
        public readonly GameSnapshot Snapshot;

        /// <summary>
        /// Discrete ops (domain-level) that must be delivered reliably.
        /// Adapter encodes these into wire ops and feeds ServerReliableStream.
        /// </summary>
        public readonly EngineOpBatch[] ReliableOps;

        public EngineTickResult(int serverTick, double serverTimeMs, GameSnapshot snapshot, EngineOpBatch[] reliableOps)
        {
            ServerTick = serverTick;
            ServerTimeMs = serverTimeMs;
            Snapshot = snapshot;
            ReliableOps = reliableOps ?? Array.Empty<EngineOpBatch>();
        }
    }

    /// <summary>
    /// Minimal continuous state for interpolation.
    /// </summary>
    public readonly struct SampleEntityPos
    {
        public readonly int EntityId;
        public readonly int X;
        public readonly int Y;
        public readonly int Hp;

        public SampleEntityPos(int entityId, int x, int y, int hp)
        {
            EntityId = entityId;
            X = x;
            Y = y;
            Hp = hp;
        }
    }

    /// <summary>
    /// Domain-level discrete op types (NOT wire OpType).
    /// Expand over time: container ops, entity spawn/despawn, status changes, etc.
    /// </summary>
    public enum EngineOpType : byte
    {
        None = 0,

        // Examples for future use:
        // MoveCard = 10,
        // RemoveEntity = 20,
        // SpawnEntity = 21,
    }

    /// <summary>
    /// Domain-level discrete op (NOT byte[] payload).
    /// Keep this deterministic and codec-friendly.
    /// </summary>
    public readonly struct EngineOp
    {
        public readonly EngineOpType Type;

        // Generic payload fields (keep small; add specialized structs later if needed).
        public readonly int A;
        public readonly int B;
        public readonly int C;
        public readonly int D;

        public EngineOp(EngineOpType type, int a = 0, int b = 0, int c = 0, int d = 0)
        {
            Type = type;
            A = a;
            B = b;
            C = c;
            D = d;
        }
    }

    /// <summary>
    /// A batch of discrete ops targeted at a connection.
    /// Use ConnId = -1 for broadcast if you want that convention later.
    /// </summary>
    public readonly struct EngineOpBatch
    {
        public readonly int ConnId;
        public readonly EngineOp[] Ops;

        public EngineOpBatch(int connId, EngineOp[] ops)
        {
            ConnId = connId;
            Ops = ops ?? Array.Empty<EngineOp>();
        }
    }
}
