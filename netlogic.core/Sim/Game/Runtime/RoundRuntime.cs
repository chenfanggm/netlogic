using Sim.Game.Flow;

namespace Sim.Game.Runtime
{
    /// <summary>
    /// Authoritative round runtime state (resets each round).
    /// </summary>
    public sealed class RoundRuntime
    {
        public RoundState State;

        /// <summary>
        /// 1..3 within the current level.
        /// </summary>
        public int RoundIndex;

        public int CustomerId;
        public int TargetScore;

        /// <summary>
        /// 0..5 (you described 5 cook chances).
        /// </summary>
        public int CookAttemptsUsed;

        public int CumulativeScore;

        // Last cook result (for client presentation triggers)
        public int LastCookSeq;
        public int LastCookScoreDelta;
        public bool LastCookMetTarget;

        /// <summary>
        /// True when round is won and player can exit to LevelOverview.
        /// </summary>
        public bool IsRoundWon;

        /// <summary>
        /// True when run is lost (attempts exhausted and target not met).
        /// </summary>
        public bool IsRunLost;

        public void ResetForNewRound()
        {
            State = RoundState.None;
            RoundIndex = 0;
            CustomerId = 0;
            TargetScore = 0;
            CookAttemptsUsed = 0;
            CumulativeScore = 0;

            LastCookSeq = 0;
            LastCookScoreDelta = 0;
            LastCookMetTarget = false;

            IsRoundWon = false;
            IsRunLost = false;
        }
    }
}
