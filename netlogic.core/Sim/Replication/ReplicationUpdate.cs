using System;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Client-facing replication envelope.
    ///
    /// ClientEngine consumes this (NOT wire messages).
    /// Carries tick/seq/hash metadata plus decoded RepOps.
    /// </summary>
    public readonly struct ReplicationUpdate
    {
        public readonly int ServerTick;
        public readonly uint ServerSeq;
        public readonly uint StateHash;
        public readonly bool IsReliable;
        public readonly RepOp[] Ops;

        public ReplicationUpdate(int serverTick, uint serverSeq, uint stateHash, bool isReliable, RepOp[] ops)
        {
            ServerTick = serverTick;
            ServerSeq = serverSeq;
            StateHash = stateHash;
            IsReliable = isReliable;
            Ops = ops ?? Array.Empty<RepOp>();
        }
    }
}
