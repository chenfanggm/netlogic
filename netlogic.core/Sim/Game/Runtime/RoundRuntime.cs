using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.sim.game.runtime
{
    /// <summary>
    /// Authoritative round runtime state (resets each round).
    /// </summary>
    public sealed class RoundRuntime
    {
        public RoundState State { get; internal set; }

        /// <summary>
        /// 1..3 within the current level.
        /// </summary>
        public int RoundIndex { get; internal set; }

        public int CustomerId { get; internal set; }
        public int TargetScore { get; internal set; }

        /// <summary>
        /// 0..5 (you described 5 cook chances).
        /// </summary>
        public int CookAttemptsUsed { get; internal set; }

        public int CumulativeScore { get; internal set; }

        // Last cook result (for client presentation triggers)
        public int LastCookSeq { get; internal set; }
        public int LastCookScoreDelta { get; internal set; }
        public bool LastCookMetTarget { get; internal set; }

        /// <summary>
        /// True when round is won and player can exit to LevelOverview.
        /// </summary>
        public bool IsRoundWon { get; internal set; }

        /// <summary>
        /// True when run is lost (attempts exhausted and target not met).
        /// </summary>
        public bool IsRunLost { get; internal set; }

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
