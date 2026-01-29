using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Transport-agnostic replication frame contract.
    /// A "frame" is one authoritative tick's worth of replication ops + metadata.
    ///
    /// Intentionally small + stable:
    /// - No wire/protocol headers
    /// - No reliability/ack state
    /// - No baseline snapshot (snapshots are separate and explicit)
    /// </summary>
    public interface IReplicationFrame
    {
        int Tick { get; }
        double ServerTimeMs { get; }
        uint StateHash { get; }
        RepOpBatch Ops { get; }
    }
}
