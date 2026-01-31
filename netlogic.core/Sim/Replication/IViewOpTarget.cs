namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// View/presentation ops only.
    /// These must NEVER be required for authoritative decisions.
    /// Client may use them for UI/events.
    /// </summary>
    public interface IViewOpTarget
    {
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
    }
}
