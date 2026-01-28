using System;

namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Canonical authoritative output for a single fixed server tick.
    /// Note: tick frames intentionally do NOT carry engine snapshots (debug-only),
    /// to keep the server path decoupled from internal snapshot types.
    /// </summary>
    public readonly struct TickFrame : IDisposable
    {
        public readonly int Tick;
        public readonly double ServerTimeMs;

        /// <summary>Hash of authoritative world state AFTER this tick.</summary>
        public readonly uint StateHash;

        /// <summary>Ordered replication ops for this tick.</summary>
        public readonly RepOpBatch Ops;

        public TickFrame(int tick, double serverTimeMs, uint stateHash, RepOpBatch ops)
        {
            Tick = tick;
            ServerTimeMs = serverTimeMs;
            StateHash = stateHash;
            Ops = ops;
        }

        /// <summary>
        /// If this frame uses pooled ops, clone them into an owned array so the frame can be stored long-term.
        /// </summary>
        public TickFrame WithOwnedOps()
        {
            if (Ops.Count == 0)
                return this;

            RepOp[] owned = Ops.ToArray();
            return new TickFrame(Tick, ServerTimeMs, StateHash, RepOpBatch.FromOwnedArray(owned, owned.Length));
        }

        public void Dispose()
        {
            Ops.Dispose();
        }
    }
}
