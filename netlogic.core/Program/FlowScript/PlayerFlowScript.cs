using System;
using com.aqua.netlogic.command.events;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.net;
using com.aqua.netlogic.program;
using com.aqua.netlogic.sim.clientengine;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.systems.gameflowsystem.commands;
using com.aqua.netlogic.sim.systems.movementsystem.commands;

namespace com.aqua.netlogic.program.flowscript
{
    /// <summary>
    /// Scripted player journey:
    /// Boot → MainMenu → RunSetup → LevelOverview → InRound (1s + move) → RunVictory → MainMenu
    /// </summary>
    public sealed class PlayerFlowScript
    {
        private readonly Action<GameFlowIntent, int> _fireIntent;
        private readonly Action _move;
        private readonly ClientEngine? _clientEngine;
        private readonly RenderSimulator? _renderSim;
        private readonly int _playerEntityId;
        private readonly RoundState _roundState = new RoundState();
        private double _lastPrintAtMs;
        private bool _selectedHat;
        private bool _wasInRound;
        private bool _completedRun;

        public PlayerFlowScript(
            IEventBus eventBus,
            ClientEngine clientEngine,
            RenderSimulator? renderSim,
            int playerEntityId)
        {
            _fireIntent = (intent, param0) =>
            {
                eventBus.Publish(new CommandEvent(
                    new FlowIntentEngineCommand(intent, param0)));
            };
            _move = () =>
            {
                eventBus.Publish(new CommandEvent(
                    new MoveByEngineCommand(entityId: playerEntityId, dx: 1, dy: 0)));
            };
            _clientEngine = clientEngine;
            _renderSim = renderSim;
            _playerEntityId = playerEntityId;
        }

        public PlayerFlowScript(Action<GameFlowIntent, int> fireIntent, Action move)
        {
            _fireIntent = fireIntent;
            _move = move;
            _clientEngine = null;
            _renderSim = null;
            _playerEntityId = -1;
        }

        private void Move()
        {
            _move();
        }

        private void FireIntent(GameFlowIntent intent, int param0)
        {
            _fireIntent(intent, param0);
        }

        private void LogInRoundState(GameFlowState gameFlowState, double nowMs)
        {
            if (gameFlowState != GameFlowState.InRound || nowMs - _lastPrintAtMs < 500)
                return;

            _lastPrintAtMs = nowMs;
            if (_clientEngine == null)
                return;

            if (_clientEngine.Model.Entities.TryGetValue(_playerEntityId, out EntityState e))
                Console.WriteLine($"[ClientModel] InRound Entity {_playerEntityId} pos=({e.X},{e.Y})");
        }

        private void ExitAfterVictoryIfNeeded(double nowMs)
        {
            if (_renderSim == null)
                return;

            if (_renderSim.ExitAfterVictoryAtMs > 0 && nowMs >= _renderSim.ExitAfterVictoryAtMs)
                FireIntent(GameFlowIntent.ReturnToMenu, 0);
        }

        public void Reset()
        {
            _roundState.Reset();
            _selectedHat = false;
            _wasInRound = false;
        }

        /// <summary>
        /// Call once per tick.
        /// </summary>
        public void Step(
            GameFlowState gameFlowState,
            double nowMs)
        {
            if (_completedRun && gameFlowState == GameFlowState.MainMenu)
                return;

            bool inRound = gameFlowState == GameFlowState.InRound;
            if (inRound)
            {
                _wasInRound = true;
            }
            else if (_wasInRound)
            {
                _wasInRound = false;
                _roundState.Reset();
            }

            switch (gameFlowState)
            {
                case GameFlowState.Boot:
                    FireIntent(GameFlowIntent.ReturnToMenu, 0);
                    break;

                case GameFlowState.MainMenu:
                    FireIntent(GameFlowIntent.ClickNewGame, 0);
                    break;

                case GameFlowState.RunSetup:
                    if (!_selectedHat)
                    {
                        FireIntent(GameFlowIntent.SelectChefHat, 1);
                        _selectedHat = true;
                    }
                    else
                    {
                        FireIntent(GameFlowIntent.ClickStartRun, 0);
                    }
                    break;

                case GameFlowState.LevelOverview:
                    FireIntent(GameFlowIntent.ClickServeCustomer, 0);
                    break;

                case GameFlowState.InRound:
                    {
                        if (_roundState.EnteredAtMs < 0)
                            _roundState.EnteredAtMs = nowMs;

                        if (_roundState.LastMoveAtMs < 0 || nowMs - _roundState.LastMoveAtMs >= 200)
                        {
                            _roundState.LastMoveAtMs = nowMs;
                            Move();
                        }

                        // After 1 second in-round, cook + continue in cycles to exit round.
                        if (nowMs - _roundState.EnteredAtMs >= 4000)
                        {
                            if (_roundState.WaitingForContinue)
                            {
                                _roundState.WaitingForContinue = false;
                                FireIntent(GameFlowIntent.ClickContinue, 0);
                                _roundState.CookCyclesCompleted++;
                            }
                            else if (_roundState.CookCyclesCompleted < 3)
                            {
                                FireIntent(GameFlowIntent.ClickCook, 0);
                                _roundState.WaitingForContinue = true;
                            }
                        }

                        break;
                    }

                case GameFlowState.RunVictory:
                    if (!_completedRun)
                        FireIntent(GameFlowIntent.ReturnToMenu, 0);
                    _completedRun = true;
                    Reset();
                    break;

                default:
                    break;
            }

            LogInRoundState(gameFlowState, nowMs);
            ExitAfterVictoryIfNeeded(nowMs);
        }
    }
}
