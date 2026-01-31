using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    public static class ClientRepOpHandlerBootstrap
    {
        public static ClientRepOpHandlers CreateDefault()
        {
            ClientRepOpHandlers table = new ClientRepOpHandlers();

            table.Register(RepOpType.FlowStateSet, new FlowStateSetHandler());

            table.Register(RepOpType.EntitySpawned, new EntitySpawnedHandler());
            table.Register(RepOpType.EntityDestroyed, new EntityDestroyedHandler());
            table.Register(RepOpType.PositionSnapshot, new PositionSnapshotHandler());

            table.Register(RepOpType.RunReset, new RunResetHandler());
            table.Register(RepOpType.RunSelectedChefHatSet, new RunSelectedChefHatSetHandler());
            table.Register(RepOpType.RunLevelIndexSet, new RunLevelIndexSetHandler());
            table.Register(RepOpType.RunSeedSet, new RunSeedSetHandler());
            table.Register(RepOpType.RunRngResetFromSeed, new RunRngResetFromSeedHandler());

            table.Register(RepOpType.LevelReset, new LevelResetHandler());
            table.Register(RepOpType.LevelRefreshesRemainingSet, new LevelRefreshesRemainingSetHandler());
            table.Register(RepOpType.LevelPendingServeCustomerIndexSet, new LevelPendingServeCustomerIndexSetHandler());
            table.Register(RepOpType.LevelCustomerIdSet, new LevelCustomerIdSetHandler());
            table.Register(RepOpType.LevelCustomerServedSet, new LevelCustomerServedSetHandler());

            table.Register(RepOpType.RoundReset, new RoundResetHandler());
            table.Register(RepOpType.RoundStateSet, new RoundStateSetHandler());
            table.Register(RepOpType.RoundRoundIndexSet, new RoundRoundIndexSetHandler());
            table.Register(RepOpType.RoundCustomerIdSet, new RoundCustomerIdSetHandler());
            table.Register(RepOpType.RoundTargetScoreSet, new RoundTargetScoreSetHandler());
            table.Register(RepOpType.RoundCookAttemptsUsedSet, new RoundCookAttemptsUsedSetHandler());
            table.Register(RepOpType.RoundCumulativeScoreSet, new RoundCumulativeScoreSetHandler());
            table.Register(RepOpType.RoundLastCookSeqSet, new RoundLastCookSeqSetHandler());
            table.Register(RepOpType.RoundLastCookScoreDeltaSet, new RoundLastCookScoreDeltaSetHandler());
            table.Register(RepOpType.RoundLastCookMetTargetSet, new RoundLastCookMetTargetSetHandler());
            table.Register(RepOpType.RoundIsRoundWonSet, new RoundIsRoundWonSetHandler());
            table.Register(RepOpType.RoundIsRunLostSet, new RoundIsRunLostSetHandler());

            table.Register(RepOpType.EntityBuffSet, new EntityBuffSetHandler());
            table.Register(RepOpType.EntityCooldownSet, new EntityCooldownSetHandler());

            return table;
        }
    }
}
