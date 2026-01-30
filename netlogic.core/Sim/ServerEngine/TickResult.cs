using System;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Canonical authoritative output for a single fixed server tick.
    ///
    /// ClientEngine can consume ServerEngine output directly without any encode/decode step.
    /// </summary>
    public readonly struct TickResult : IDisposable
    {
        public readonly int Tick;
        public readonly double ServerTimeMs;

        /// <summary>Hash of authoritative world state AFTER this tick.</summary>
        public readonly uint StateHash;

        /// <summary>Ordered replication ops for this tick.</summary>
        public readonly RepOpBatch Ops;

        /// <summary>Optional snapshot captured for this tick.</summary>
        public readonly ServerModelSnapshot? Snapshot;

        public bool HasSnapshot => Snapshot != null;

        public TickResult(
            int tick,
            double serverTimeMs,
            uint stateHash,
            RepOpBatch ops,
            ServerModelSnapshot? snapshot = null)
        {
            Tick = tick;
            ServerTimeMs = serverTimeMs;
            StateHash = stateHash;
            Ops = ops;
            Snapshot = snapshot;
        }

        /// <summary>
        /// If this result uses pooled ops, clone them into an owned array so the result can be stored long-term.
        /// </summary>
        public TickResult WithOwnedOps()
        {
            if (Ops.Count == 0)
                return this;

            RepOp[] owned = Ops.ToArray();
            return new TickResult(
                Tick,
                ServerTimeMs,
                StateHash,
                RepOpBatch.FromOwnedArray(owned, owned.Length),
                Snapshot);
        }

        /// <summary>
        /// Backwards-compat alias for WithOwnedOps().
        /// </summary>
        public TickResult Clone() => WithOwnedOps();

        public void Dispose()
        {
            Ops.Dispose();
        }
    }
}
