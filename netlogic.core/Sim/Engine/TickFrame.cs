using Sim.Snapshot;

namespace Sim.Engine
{
    /// <summary>
    /// Canonical authoritative output for a single fixed server tick.
    ///
    /// Design goals:
    /// - Same object worksTickFrame for local (in-process) and remote (serialized) delivery.
    /// - Ops are the stable render/network boundary (NOT the internal EventBus).
    /// - Snapshot is optional (periodic / on-demand), but can be present every tick for now.
    /// - StateHash enables cheap desync detection and replay verification.
    /// </summary>
    public readonly struct TickFrame
    {
        public readonly int Tick;
        public readonly double ServerTimeMs;

        /// <summary>
        /// Hash of authoritative world state AFTER this tick is applied.
        /// </summary>
        public readonly uint StateHash;

        /// <summary>
        /// Discrete replication ops for this tick (ordered, deterministic).
        /// These are the stable boundary for remote sync and local render consumption.
        /// </summary>
        public readonly RepOp[] Ops;

        /// <summary>
        /// Optional snapshot payload (baseline / late-join / UI convenience).
        /// Can be null most ticks if you choose to reduce bandwidth later.
        /// </summary>
        public readonly GameSnapshot? Snapshot;

        public TickFrame(int tick, double serverTimeMs, uint stateHash, RepOp[] ops, GameSnapshot? snapshot)
        {
            Tick = tick;
            ServerTimeMs = serverTimeMs;
            StateHash = stateHash;
            Ops = ops ?? Array.Empty<RepOp>();
            Snapshot = snapshot;
        }
    }

    /// <summary>
    /// Replication op types (generic, render/network-facing; not domain events).
    /// Expand conservatively.
    /// </summary>
    public enum RepOpType : byte
    {
        None = 0,

        // View-state deltas
        PositionAt = 50,

        // Flow / UI triggers (optional, reliable)
        FlowFire = 60,
    }

    /// <summary>
    /// Small fixed-width replication op.
    /// Keep codec-friendly, deterministic, and ordered.
    /// If you need richer payloads later, add dedicated op structs or a blob table.
    /// </summary>
    public readonly struct RepOp
    {
        public readonly RepOpType Type;
        public readonly int A;
        public readonly int B;
        public readonly int C;
        public readonly int D;

        public RepOp(RepOpType type, int a = 0, int b = 0, int c = 0, int d = 0)
        {
            Type = type;
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public static RepOp PositionAt(int entityId, int x, int y) => new RepOp(RepOpType.PositionAt, entityId, x, y);

        public static RepOp FlowFire(byte trigger) => new RepOp(RepOpType.FlowFire, trigger);
    }

    /// <summary>
    /// Per-tick recorder for replication ops.
    /// This is the canonical place to collect ops (NOT the EventBus).
    /// </summary>
    public interface IReplicationRecorder
    {
        void BeginTick(int tick);
        void Record(in RepOp op);
        RepOp[] EndTickAndFlush();
    }

    public sealed class ReplicationRecorder : IReplicationRecorder
    {
        private readonly List<RepOp> _ops;
        private int _tick;

        public ReplicationRecorder(int initialCapacity = 128)
        {
            _ops = new List<RepOp>(Math.Max(8, initialCapacity));
            _tick = 0;
        }

        public void BeginTick(int tick)
        {
            _tick = tick;
            _ops.Clear();
        }

        public void Record(in RepOp op)
        {
            // Keep deterministic ordering by recording in mutation order.
            _ops.Add(op);
        }

        public RepOp[] EndTickAndFlush()
        {
            // NOTE: If you want pooling, replace ToArray() with an ArrayPool-backed copy.
            return _ops.Count == 0 ? Array.Empty<RepOp>() : _ops.ToArray();
        }
    }
}
