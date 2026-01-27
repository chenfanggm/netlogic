using MemoryPack;

namespace Net.WireState
{
    [MemoryPackable]
    public readonly partial struct WireFlowState
    {
        public readonly int FlowState;
        public readonly int LevelIndex;
        public readonly int RoundIndex;
        public readonly int SelectedChefHatId;
        public readonly int TargetScore;
        public readonly int CumulativeScore;
        public readonly int CookAttemptsUsed;
        public readonly int RoundState;
        public readonly int CookResultSeq;
        public readonly int LastCookScoreDelta;
        public readonly bool LastCookMetTarget;

        public WireFlowState(
            int flowState,
            int levelIndex,
            int roundIndex,
            int selectedChefHatId,
            int targetScore,
            int cumulativeScore,
            int cookAttemptsUsed,
            int roundState,
            int cookResultSeq,
            int lastCookScoreDelta,
            bool lastCookMetTarget)
        {
            FlowState = flowState;
            LevelIndex = levelIndex;
            RoundIndex = roundIndex;
            SelectedChefHatId = selectedChefHatId;
            TargetScore = targetScore;
            CumulativeScore = cumulativeScore;
            CookAttemptsUsed = cookAttemptsUsed;
            RoundState = roundState;
            CookResultSeq = cookResultSeq;
            LastCookScoreDelta = lastCookScoreDelta;
            LastCookMetTarget = lastCookMetTarget;
        }
    }
}
