namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Canonical authoritative output for a single fixed server tick.
    /// Note: tick frames intentionally do NOT carry engine snapshots (debug-only),
    /// to keep the server path decoupled from internal snapshot types.
    /// </summary>
    public readonly struct TickFrame(int tick, double serverTimeMs, uint worldHash, RepOpBatch ops) : IDisposable
    {
        public readonly int Tick = tick;
        public readonly double ServerTimeMs = serverTimeMs;

        /// <summary>Hash of authoritative world state AFTER this tick.</summary>
        public readonly uint WorldHash = worldHash;

        /// <summary>Ordered replication ops for this tick.</summary>
        public readonly RepOpBatch Ops = ops;

        public void Dispose()
        {
            Ops.Dispose();
        }

        /// <summary>
        /// If this frame uses pooled ops, clone them into an owned array so the frame can be stored long-term.
        /// </summary>
        public TickFrame Clone()
        {
            if (Ops.Count == 0)
                return this;

            RepOp[] owned = Ops.ToArray();
            return new TickFrame(Tick, ServerTimeMs, WorldHash, RepOpBatch.FromOwnedArray(owned, owned.Length));
        }
    }
}
