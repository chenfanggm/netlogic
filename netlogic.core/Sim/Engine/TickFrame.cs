using System;
using Sim.Snapshot;

namespace Sim.Engine
{
    /// <summary>
    /// Canonical authoritative output for a single fixed server tick.
    /// </summary>
    public readonly struct TickFrame : IDisposable
    {
        public readonly int Tick;
        public readonly double ServerTimeMs;

        /// <summary>Hash of authoritative world state AFTER this tick.</summary>
        public readonly uint StateHash;

        /// <summary>Ordered replication ops for this tick.</summary>
        public readonly RepOpBatch Ops;

        /// <summary>Optional snapshot (baseline / late-join / UI convenience).</summary>
        public readonly GameSnapshot? Snapshot;

        public TickFrame(int tick, double serverTimeMs, uint stateHash, RepOpBatch ops, GameSnapshot? snapshot)
        {
            Tick = tick;
            ServerTimeMs = serverTimeMs;
            StateHash = stateHash;
            Ops = ops;
            Snapshot = snapshot;
        }

        /// <summary>
        /// If this frame uses pooled ops, clone them into an owned array so the frame can be stored long-term.
        /// </summary>
        public TickFrame WithOwnedOps()
        {
            if (Ops.Count == 0)
                return this;

            // Clone to an owned array (safe to store), keep same snapshot reference.
            RepOp[] owned = Ops.ToArray();
            return new TickFrame(Tick, ServerTimeMs, StateHash, RepOpBatch.FromOwnedArray(owned, owned.Length), Snapshot);
        }

        public void Dispose()
        {
            Ops.Dispose();
        }
    }
}
