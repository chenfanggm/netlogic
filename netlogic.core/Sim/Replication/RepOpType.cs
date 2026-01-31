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

        // Flow / runtime ops (authoritative)
        FlowStateSet = 100,

        RunReset = 110,
        RunSelectedChefHatSet = 111,
        RunLevelIndexSet = 112,
        RunSeedSet = 113,
        RunRngResetFromSeed = 114,

        LevelReset = 120,
        LevelRefreshesRemainingSet = 121,
        LevelPendingServeCustomerIndexSet = 122,
        LevelCustomerIdSet = 123,
        LevelCustomerServedSet = 124,

        RoundReset = 130,
        RoundStateSet = 131,
        RoundRoundIndexSet = 132,
        RoundCustomerIdSet = 133,
        RoundTargetScoreSet = 134,
        RoundCookAttemptsUsedSet = 135,
        RoundCumulativeScoreSet = 136,
        RoundLastCookSeqSet = 137,
        RoundLastCookScoreDeltaSet = 138,
        RoundLastCookMetTargetSet = 139,
        RoundIsRoundWonSet = 140,
        RoundIsRunLostSet = 141,

        // Entity runtime (authoritative)
        EntityBuffSet = 200,
        EntityCooldownSet = 201,
    }
}
