using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Client-facing replication envelope.
    /// This is what ClientEngine consumes (NOT wire messages).
    /// </summary>
    public readonly struct ReplicationUpdate
    {
        public readonly int ServerTick;
        public readonly uint ServerSeq;   // 0 allowed (unreliable lane or "no seq")
        public readonly uint StateHash;
        public readonly bool IsReliable;
        public readonly RepOp[] Ops;      // empty array = heartbeat update

        public ReplicationUpdate(int serverTick, uint serverSeq, uint stateHash, bool isReliable, RepOp[] ops)
        {
            ServerTick = serverTick;
            ServerSeq = serverSeq;
            StateHash = stateHash;
            IsReliable = isReliable;
            Ops = ops ?? global::System.Array.Empty<RepOp>();
        }
    }
}
