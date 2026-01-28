using System;
using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.sim.game.snapshot
{
    /// <summary>
    /// Compact authoritative snapshot of game flow + run/level/round runtime.
    /// Designed for client UI and deterministic replay checks.
    /// </summary>
    public readonly struct FlowSnapshot : IEquatable<FlowSnapshot>
    {
        public readonly GameFlowState FlowState;

        public readonly int LevelIndex;
        public readonly int RoundIndex;

        public readonly int SelectedChefHatId;

        public readonly int TargetScore;
        public readonly int CumulativeScore;
        public readonly int CookAttemptsUsed;

        public readonly RoundState RoundState;

        /// <summary>
        /// Monotonic sequence increments each time a cook result is computed.
        /// Client uses this to trigger score animations exactly once.
        /// </summary>
        public readonly int CookResultSeq;

        /// <summary>
        /// Last cook delta and whether target was met after that cook.
        /// Useful for UI emphasis; client can ignore if it wants.
        /// </summary>
        public readonly int LastCookScoreDelta;
        public readonly bool LastCookMetTarget;

        public FlowSnapshot(
            GameFlowState flowState,
            int levelIndex,
            int roundIndex,
            int selectedChefHatId,
            int targetScore,
            int cumulativeScore,
            int cookAttemptsUsed,
            RoundState roundState,
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

        public bool Equals(FlowSnapshot other)
        {
            return FlowState == other.FlowState
                && LevelIndex == other.LevelIndex
                && RoundIndex == other.RoundIndex
                && SelectedChefHatId == other.SelectedChefHatId
                && TargetScore == other.TargetScore
                && CumulativeScore == other.CumulativeScore
                && CookAttemptsUsed == other.CookAttemptsUsed
                && RoundState == other.RoundState
                && CookResultSeq == other.CookResultSeq
                && LastCookScoreDelta == other.LastCookScoreDelta
                && LastCookMetTarget == other.LastCookMetTarget;
        }

        public override bool Equals(object? obj) => obj is FlowSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(FlowState);
            hash.Add(LevelIndex);
            hash.Add(RoundIndex);
            hash.Add(SelectedChefHatId);
            hash.Add(TargetScore);
            hash.Add(CumulativeScore);
            hash.Add(CookAttemptsUsed);
            hash.Add(RoundState);
            hash.Add(CookResultSeq);
            hash.Add(LastCookScoreDelta);
            hash.Add(LastCookMetTarget);
            return hash.ToHashCode();
        }

        public static bool operator ==(FlowSnapshot left, FlowSnapshot right) => left.Equals(right);
        public static bool operator !=(FlowSnapshot left, FlowSnapshot right) => !left.Equals(right);
    }
}
