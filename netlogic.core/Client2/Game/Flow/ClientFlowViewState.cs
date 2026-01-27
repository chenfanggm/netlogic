using Sim.Game.Flow;

namespace Client2.Game.Flow
{
    public sealed class ClientFlowViewState
    {
        public GameFlowState FlowState { get; private set; }
        public RoundState RoundState { get; private set; }
        public bool LastCookMetTarget { get; private set; }
        public int CookAttemptsUsed { get; private set; }
        public int LevelIndex { get; private set; }
        public int RoundIndex { get; private set; }
        public int SelectedChefHatId { get; private set; }
        public int TargetScore { get; private set; }
        public int CumulativeScore { get; private set; }
        public int CookResultSeq { get; private set; }
        public int LastCookScoreDelta { get; private set; }

        public void ApplyFlowSnapshot(
            byte flowState,
            byte roundState,
            byte lastMetTarget,
            byte cookAttemptsUsed,
            int levelIndex,
            int roundIndex,
            int selectedChefHatId,
            int targetScore,
            int cumulativeScore,
            int cookResultSeq,
            int lastCookScoreDelta)
        {
            FlowState = (GameFlowState)flowState;
            RoundState = (RoundState)roundState;
            LastCookMetTarget = lastMetTarget != 0;
            CookAttemptsUsed = cookAttemptsUsed;
            LevelIndex = levelIndex;
            RoundIndex = roundIndex;
            SelectedChefHatId = selectedChefHatId;
            TargetScore = targetScore;
            CumulativeScore = cumulativeScore;
            CookResultSeq = cookResultSeq;
            LastCookScoreDelta = lastCookScoreDelta;
        }
    }
}
