using System;
using Sim.Game.Flow;

namespace Program.FlowScript
{
    /// <summary>
    /// Scripted player journey:
    /// Boot → MainMenu → RunSetup → LevelOverview → InRound (1s + move) → RunVictory → MainMenu
    /// </summary>
    public sealed class PlayerFlowScript
    {
        private long _enteredInRoundAtMs = -1;
        private long _lastMoveAtMs = -1;
        private int _cookCyclesCompleted;
        private bool _waitingForContinue;
        private bool _selectedHat;
        private bool _wasInRound;
        private bool _completedRun;

        public void Reset()
        {
            _enteredInRoundAtMs = -1;
            _lastMoveAtMs = -1;
            _cookCyclesCompleted = 0;
            _waitingForContinue = false;
            _selectedHat = false;
            _wasInRound = false;
        }

        /// <summary>
        /// Call once per tick.
        /// fireIntent: flow UI actions (start/confirm)
        /// move: gameplay action while InRound
        /// </summary>
        public void Step(
            GameFlowState flowState,
            long nowMs,
            Action<GameFlowIntent, int> fireIntent,
            Action move)
        {
            if (_completedRun && flowState == GameFlowState.MainMenu)
                return;

            switch (flowState)
            {
                case GameFlowState.InRound:
                    _wasInRound = true;
                    break;

                default:
                    if (_wasInRound)
                    {
                        _wasInRound = false;
                        _enteredInRoundAtMs = -1;
                        _lastMoveAtMs = -1;
                        _cookCyclesCompleted = 0;
                        _waitingForContinue = false;
                    }
                    break;
            }

            switch (flowState)
            {
                case GameFlowState.Boot:
                    fireIntent(GameFlowIntent.ReturnToMenu, 0);
                    break;

                case GameFlowState.MainMenu:
                    fireIntent(GameFlowIntent.ClickNewGame, 0);
                    break;

                case GameFlowState.RunSetup:
                    if (!_selectedHat)
                    {
                        fireIntent(GameFlowIntent.SelectChefHat, 1);
                        _selectedHat = true;
                    }
                    else
                    {
                        fireIntent(GameFlowIntent.ClickStartRun, 0);
                    }
                    break;

                case GameFlowState.LevelOverview:
                    fireIntent(GameFlowIntent.ClickServeCustomer, 0);
                    break;

                case GameFlowState.InRound:
                {
                    if (_enteredInRoundAtMs < 0)
                        _enteredInRoundAtMs = nowMs;

                    if (_lastMoveAtMs < 0 || nowMs - _lastMoveAtMs >= 200)
                    {
                        _lastMoveAtMs = nowMs;
                        move();
                    }

                    // After 1 second in-round, cook + continue in cycles to exit round.
                    if (nowMs - _enteredInRoundAtMs >= 1000)
                    {
                        if (_waitingForContinue)
                        {
                            _waitingForContinue = false;
                            fireIntent(GameFlowIntent.ClickContinue, 0);
                            _cookCyclesCompleted++;
                        }
                        else if (_cookCyclesCompleted < 3)
                        {
                            fireIntent(GameFlowIntent.ClickCook, 0);
                            _waitingForContinue = true;
                        }
                    }

                    break;
                }

                case GameFlowState.RunVictory:
                    if (!_completedRun)
                        fireIntent(GameFlowIntent.ReturnToMenu, 0);
                    _completedRun = true;
                    Reset();
                    break;

                default:
                    break;
            }
        }
    }
}
