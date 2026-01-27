using Stateless;

namespace Sim.Game.Flow
{
    public sealed class GameFlowController
    {
        private readonly Game _world;
        private readonly StateMachine<GameFlowState, GameFlowIntent> _sm;

        public GameFlowController(Game world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));

            _sm = new StateMachine<GameFlowState, GameFlowIntent>(
                stateAccessor: () => _world.FlowManager.State,
                stateMutator: s => _world.FlowManager.SetStateInternal(s));

            Configure(_sm);
        }

        public GameFlowState State => _world.FlowManager.State;

        internal void ApplyPlayerIntentFromCommand(GameFlowIntent intent, int param0)
        {
            if (intent == GameFlowIntent.None)
                return;

            // Intent payload writes (authoritative, deterministic)
            switch (intent)
            {
                case GameFlowIntent.SelectChefHat:
                    _world.Run.SelectedChefHatId = param0;
                    break;

                case GameFlowIntent.ClickServeCustomer:
                    // store the pending index; the actual round init happens on entering InRound
                    _world.Level.PendingServeCustomerIndex = param0;
                    break;

                default:
                    break;
            }

            // Handle intents that are "local" to round without necessarily changing GameFlowState.
            // This keeps top-level flow stable and round loop clean.
            if (_world.FlowManager.State == GameFlowState.InRound)
            {
                if (intent == GameFlowIntent.ClickCook)
                {
                    _world.RoundFlow.ApplyCook();
                    return; // no top-level transition
                }

                if (intent == GameFlowIntent.ClickContinue)
                {
                    bool shouldExitRound = _world.RoundFlow.ApplyContinue();

                    if (_world.Round.IsRunLost)
                    {
                        // continue from a losing outcome: go to defeat
                        if (_sm.CanFire(GameFlowIntent.ClickConcedeRun))
                            _sm.Fire(GameFlowIntent.ClickConcedeRun);
                        return;
                    }

                    if (shouldExitRound)
                    {
                        // For harness: any won round exits to RunVictory.
                        if (_world.Round.IsRoundWon)
                        {
                            _world.FlowManager.SetStateInternal(GameFlowState.RunVictory);
                            return;
                        }

                        if (_sm.CanFire(GameFlowIntent.ClickContinue))
                        {
                            // exit back to LevelOverview (won round)
                            _sm.Fire(GameFlowIntent.ClickContinue);
                        }
                    }
                    return;
                }
            }

            // Validate StartRun requires hat
            if (intent == GameFlowIntent.ClickStartRun && _world.Run.SelectedChefHatId == 0)
                return;

            if (!_sm.CanFire(intent))
                return;

            _sm.Fire(intent);
        }

        private static void Configure(StateMachine<GameFlowState, GameFlowIntent> sm)
        {
            sm.Configure(GameFlowState.Boot)
              .Permit(GameFlowIntent.ReturnToMenu, GameFlowState.MainMenu);

            sm.Configure(GameFlowState.MainMenu)
              .Permit(GameFlowIntent.ClickNewGame, GameFlowState.RunSetup);

            sm.Configure(GameFlowState.RunSetup)
              .PermitReentry(GameFlowIntent.SelectChefHat)
              .Permit(GameFlowIntent.ClickStartRun, GameFlowState.LevelOverview)
              .Permit(GameFlowIntent.ReturnToMenu, GameFlowState.MainMenu);

            sm.Configure(GameFlowState.LevelOverview)
              .Permit(GameFlowIntent.ClickServeCustomer, GameFlowState.InRound)
              .Permit(GameFlowIntent.ClickConcedeRun, GameFlowState.RunDefeat)
              .Permit(GameFlowIntent.ReturnToMenu, GameFlowState.MainMenu);

            // leaving round (won) uses ClickContinue to go back to LevelOverview
            sm.Configure(GameFlowState.InRound)
              .Permit(GameFlowIntent.ClickContinue, GameFlowState.LevelOverview)
              .Permit(GameFlowIntent.ClickConcedeRun, GameFlowState.RunDefeat)
              .Permit(GameFlowIntent.ReturnToMenu, GameFlowState.MainMenu);

            sm.Configure(GameFlowState.RunDefeat)
              .Permit(GameFlowIntent.ReturnToMenu, GameFlowState.MainMenu);

            sm.Configure(GameFlowState.RunVictory)
              .Permit(GameFlowIntent.ReturnToMenu, GameFlowState.MainMenu);
        }
    }
}
