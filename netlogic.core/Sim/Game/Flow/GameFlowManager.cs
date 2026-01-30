using com.aqua.netlogic.sim.game.runtime;

namespace com.aqua.netlogic.sim.game.flow
{
    /// <summary>
    /// Owns authoritative flow state and deterministic enter/exit hooks.
    /// </summary>
    public sealed class GameFlowManager
    {
        private readonly ServerModel _world;

        // Lifecycle tracking (deterministic, internal)
        private GameFlowState _prevFlowState = (GameFlowState)255;

        public GameFlowState State { get; private set; } = GameFlowState.Boot;

        public GameFlowManager(ServerModel world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        internal void SetStateInternal(GameFlowState state)
        {
            State = state;
        }

        // ------------------------------------------------------------------
        // Deterministic lifecycle: runs every tick, reacts to FlowState changes
        // ------------------------------------------------------------------

        internal void LifecycleStep()
        {
            if (_prevFlowState != State)
            {
                OnExitFlowState(_prevFlowState);
                OnEnterFlowState(State);
                _prevFlowState = State;
            }

            // You can add other deterministic per-tick checks here if needed.
            // For example: auto-advance level when all 3 customers served and last round won.
        }

        private void OnExitFlowState(GameFlowState state)
        {
            // Keep exit actions minimal; most work should be on enter.
            _ = state;
        }

        private void OnEnterFlowState(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Boot:
                    // No work. We usually transition to MainMenu by client or by a boot command.
                    break;

                case GameFlowState.MainMenu:
                    // Reset everything for a clean slate
                    ResetAllRuntime();
                    break;

                case GameFlowState.RunSetup:
                    // Prepare a fresh run setup selection screen.
                    ResetAllRuntime();
                    // Player will choose hat -> Run.SelectedChefHatId
                    break;

                case GameFlowState.LevelOverview:
                    // If this is a new run start or a new level, ensure level data exists.
                    // We (re)build level preview customers only when needed.
                    EnsureLevelInitialized();
                    break;

                case GameFlowState.InRound:
                    // When player clicks Serve, Level.PendingServeCustomerIndex is set.
                    InitRoundFromPendingServe();
                    break;

                case GameFlowState.RunDefeat:
                    // Nothing required; UI will show defeat.
                    break;

                case GameFlowState.RunVictory:
                    // Nothing required; UI will show victory.
                    break;

                default:
                    break;
            }
        }

        private void ResetAllRuntime()
        {
            _world.Run.SelectedChefHatId = 0;
            _world.Run.LevelIndex = 0;
            _world.Run.RunSeed = 0;
            _world.Run.Rng = new DeterministicRng(1);

            _world.Level.ResetForNewLevel();
            _world.Round.ResetForNewRound();
        }

        private void EnsureLevelInitialized()
        {
            // If level index is 0, this is the first time entering a run.
            if (_world.Run.LevelIndex <= 0)
            {
                _world.Run.LevelIndex = 1;

                // Seed can be derived deterministically; here we use tick as a placeholder.
                // In production you probably set seed when starting the run (ClickStartRun).
                _world.Run.RunSeed = (uint)(_world.CurrentTick + 12345);
                _world.Run.Rng = new DeterministicRng(_world.Run.RunSeed);
            }

            // If customer preview not set, generate 3 customers deterministically
            bool needsCustomers = _world.Level.CustomerIds[0] == 0
                                  || _world.Level.CustomerIds[1] == 0
                                  || _world.Level.CustomerIds[2] == 0;
            if (needsCustomers)
            {
                // Refresh pool per level — tune this
                _world.Level.RefreshesRemaining = 3;

                // Deterministic customer IDs. Replace with weighted roll from data.
                for (int i = 0; i < 3; i++)
                {
                    // Example: base 1000 + level bias + rng
                    int id = 1000 + _world.Run.LevelIndex * 10 + _world.Run.Rng.NextInt(0, 10);
                    _world.Level.CustomerIds[i] = id;
                    _world.Level.Served[i] = false;
                }

                _world.Level.PendingServeCustomerIndex = -1;
            }
        }

        private void InitRoundFromPendingServe()
        {
            int idx = _world.Level.PendingServeCustomerIndex;

            // Validate serve index
            if (idx < 0 || idx >= 3)
            {
                // Invalid selection; fallback: choose first unserved
                idx = FirstUnservedCustomerIndexOrMinus1();
            }

            if (idx < 0)
            {
                // No available customer; treat as level completion placeholder.
                // For now, just keep LevelOverview.
                SetStateInternal(GameFlowState.LevelOverview);
                return;
            }

            // Mark served and clear pending
            _world.Level.Served[idx] = true;
            _world.Level.PendingServeCustomerIndex = -1;

            // Initialize round runtime
            _world.Round.ResetForNewRound();

            _world.Round.RoundIndex = _world.Level.ServedCount(); // after marking served, count is 1..3
            _world.Round.CustomerId = _world.Level.CustomerIds[idx];

            // Target curve placeholder: tune based on your difficulty system
            _world.Round.TargetScore = ComputeTargetScore(_world.Run.LevelIndex, _world.Round.RoundIndex);

            _world.Round.State = RoundState.Prepare;
        }

        private int FirstUnservedCustomerIndexOrMinus1()
        {
            for (int i = 0; i < 3; i++)
                if (!_world.Level.Served[i]) return i;
            return -1;
        }

        private static int ComputeTargetScore(int levelIndex, int roundIndex)
        {
            // Placeholder difficulty curve:
            // Round 1 easy, Round 2 medium, Round 3 boss
            int baseTarget = 120 + (levelIndex - 1) * 25;
            int roundAdd = roundIndex switch
            {
                1 => 0,
                2 => 40,
                3 => 80,
                _ => 0
            };
            return baseTarget + roundAdd;
        }
    }
}
