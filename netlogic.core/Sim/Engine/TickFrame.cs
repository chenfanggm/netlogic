using Sim.Snapshot;

namespace Sim.Engine
{
    /// <summary>
    /// Canonical authoritative output for a single fixed server tick.
    /// </summary>
    public readonly struct TickFrame
    {
        public readonly int Tick;
        public readonly double ServerTimeMs;

        /// <summary>Hash of authoritative world state AFTER this tick.</summary>
        public readonly uint StateHash;

        /// <summary>Ordered replication ops for this tick.</summary>
        public readonly RepOp[] Ops;

        /// <summary>Optional snapshot (baseline / late-join / UI convenience).</summary>
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
}
