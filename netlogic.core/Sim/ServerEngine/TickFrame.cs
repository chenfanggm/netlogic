using System;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Canonical authoritative output for a single fixed server tick.
    ///
    /// Also implements IReplicationFrame so ClientEngine can consume ServerEngine output
    /// directly without any encode/decode step.
    /// </summary>
    public readonly struct TickFrame : IDisposable, IReplicationFrame
    {
        public readonly int Tick;
        public readonly double ServerTimeMs;

        /// <summary>Hash of authoritative world state AFTER this tick.</summary>
        public readonly uint StateHash;

        /// <summary>Ordered replication ops for this tick.</summary>
        public readonly RepOpBatch Ops;

        /// <summary>Optional snapshot captured for this tick.</summary>
        public readonly GameSnapshot? Snapshot;

        // IReplicationFrame (explicit) - keeps the contract stable even if fields change.
        int IReplicationFrame.Tick => Tick;
        double IReplicationFrame.ServerTimeMs => ServerTimeMs;
        uint IReplicationFrame.StateHash => StateHash;
        RepOpBatch IReplicationFrame.Ops => Ops;

        public TickFrame(
            int tick,
            double serverTimeMs,
            uint stateHash,
            RepOpBatch ops,
            GameSnapshot? snapshot = null)
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

            RepOp[] owned = Ops.ToArray();
            return new TickFrame(
                Tick,
                ServerTimeMs,
                StateHash,
                RepOpBatch.FromOwnedArray(owned, owned.Length),
                Snapshot);
        }

        /// <summary>
        /// Backwards-compat alias for WithOwnedOps().
        /// </summary>
        public TickFrame Clone() => WithOwnedOps();

        public void Dispose()
        {
            Ops.Dispose();
        }
    }
}
