namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Authoritative operation types.
    /// Stable contract between server and client simulation cores.
    /// </summary>
    public enum RepOpType : byte
    {
        None = 0,

        // Flow (authoritative)
        FlowFire = 10,
        FlowSnapshot = 11,

        // Entity lifecycle (authoritative)
        EntitySpawned = 20,
        EntityDestroyed = 21,

        // Transforms / snapshots (presentation or authoritative, depending on rules)
        PositionSnapshot = 50,
    }
}
