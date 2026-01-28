namespace com.aqua.netlogic.sim.game.flow
{
    /// <summary>
    /// Pure round flow rules. Reads/writes World.Round runtime state.
    /// Does not own hidden state; safe to rebuild.
    /// </summary>
    public sealed class RoundFlowController
    {
        private readonly Game _world;

        public RoundFlowController(Game world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RoundState State => _world.Round.State;

        public bool CanCook()
        {
            return _world.FlowManager.State == GameFlowState.InRound
                   && _world.Round.State == RoundState.Prepare
                   && !_world.Round.IsRunLost
                   && !_world.Round.IsRoundWon;
        }

        public void ApplyCook()
        {
            if (!CanCook())
                return;

            // Consume one cook attempt
            _world.Round.CookAttemptsUsed++;

            // ------------------------------------------------------------------
            // TODO: Replace this placeholder scoring with your real satisfaction math:
            // - base dish type from first ingredient
            // - tier bonuses
            // - trait synergy
            // - customer bonuses
            // - named recipe
            // - hat/tools modifiers
            //
            // IMPORTANT: scoring must be fully deterministic based on World state.
            // ------------------------------------------------------------------
            int scoreDelta = 50; // placeholder
            _world.Round.LastCookScoreDelta = scoreDelta;
            _world.Round.CumulativeScore += scoreDelta;

            _world.Round.LastCookSeq++;
            _world.Round.LastCookMetTarget = _world.Round.CumulativeScore >= _world.Round.TargetScore;

            if (_world.Round.LastCookMetTarget)
            {
                _world.Round.IsRoundWon = true;
                _world.Round.State = RoundState.OutcomeReady;
                return;
            }

            // Not met target
            if (_world.Round.CookAttemptsUsed >= 5)
            {
                _world.Round.IsRunLost = true;
                _world.Round.State = RoundState.OutcomeReady;
                return;
            }

            // Still has attempts left; show outcome and wait for Continue
            _world.Round.State = RoundState.OutcomeReady;
        }

        /// <summary>
        /// Continue pressed in outcome screen.
        /// Returns true if the round should exit back to LevelOverview.
        /// Returns false if the round continues (back to Prepare for next cook).
        /// </summary>
        public bool ApplyContinue()
        {
            if (_world.FlowManager.State != GameFlowState.InRound)
                return false;

            if (_world.Round.State != RoundState.OutcomeReady)
                return false;

            if (_world.Round.IsRunLost)
            {
                // Run will transition to RunDefeat at GameFlow level by GameFlowController.
                return true;
            }

            if (_world.Round.IsRoundWon)
            {
                // Exit to LevelOverview.
                return true;
            }

            // Not won, attempts remain: go back to prepare for next cook.
            _world.Round.State = RoundState.Prepare;
            return false;
        }
    }
}
