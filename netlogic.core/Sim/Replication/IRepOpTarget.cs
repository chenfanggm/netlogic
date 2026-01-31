namespace com.aqua.netlogic.sim.replication
{
    public interface IRepOpTarget
    {
        void ApplyEntitySpawned(int entityId, int x, int y, int hp);
        void ApplyEntityDestroyed(int entityId);
        void ApplyPositionSnapshot(int entityId, int x, int y);

        void ApplyFlowSnapshot(
            byte flowState,
            byte roundState,
            byte lastCookMetTarget,
            byte cookAttemptsUsed,
            int levelIndex,
            int roundIndex,
            int selectedChefHatId,
            int targetScore,
            int cumulativeScore,
            int cookResultSeq,
            int lastCookScoreDelta);

        void ApplyFlowFire(byte trigger, int param0);

        void ApplyEntityBuffSet(int entityId, com.aqua.netlogic.sim.game.rules.BuffType buff, int remainingTicks);
        void ApplyEntityCooldownSet(int entityId, com.aqua.netlogic.sim.game.rules.CooldownType cd, int remainingTicks);
    }
}
