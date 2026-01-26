using Sim.Command;
using Sim.Game;
using Sim.Time;

namespace Sim.Engine
{
    /// <summary>
    /// Pure authoritative simulation core.
    /// Responsibilities:
    /// - Own World mutation
    /// - Own authoritative tick counter
    /// - Execute systems in stable order
    /// - Produce snapshots
    ///
    /// Non-responsibilities:
    /// - Threading, sleeping, realtime scheduling (TickRunner/Host)
    /// - Input generation policies (InputPump)
    /// - Output formatting/printing (SnapshotFormatter/OutputPump)
    /// </summary>
    public sealed class GameEngine : IGameEngine
    {
        public Game.Game ReadOnlyWorld => _game;

        public int CurrentTick => _currentTick;

        public double ServerTimeMs => _lastServerTimeMs;

        private int _currentTick;
        private double _lastServerTimeMs;

        private readonly Game.Game _game;
        private readonly CommandSystem<EngineCommandType> _commandSystem;
        private readonly ICommandSink<EngineCommandType>[] _commandSinks;

        public GameEngine(Game.Game initialGame)
        {
            _game = initialGame ?? throw new ArgumentNullException(nameof(initialGame));

            // Stable system execution order matters for determinism.
            // GameFlowSystem first: flow transitions happen before gameplay systems.
            GameFlowSystem flow = new GameFlowSystem();
            MovementSystem movement = new MovementSystem();
            _commandSinks = [
                flow,
                movement
            ];
            _commandSystem = new CommandSystem<EngineCommandType>(
                _commandSinks,
                maxFutureTicks: 2,
                maxPastTicks: 2,
                maxStoredTicks: 16);

            _currentTick = 0;
            _lastServerTimeMs = 0;
        }

        /// <summary>
        /// Execute exactly one authoritative tick.
        /// TickContext is provided by the runner (rate/time), but authoritative tick is owned by engine.
        /// </summary>
        public EngineTickResult TickOnce(TickContext ctx)
        {
            int tick = ++_currentTick;
            _lastServerTimeMs = ctx.ServerTimeMs;

            // 1) Execute systems in stable order
            _commandSystem.Execute(tick, _game);

            // 2) Game fixed step
            _game.Advance(1);

            return new EngineTickResult(
                serverTick: _currentTick,
                serverTimeMs: _lastServerTimeMs,
                snapshot: _game.BuildSnapshot(),
                reliableOps: []);
        }

        public void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<EngineCommand<EngineCommandType>> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            _commandSystem.Enqueue(
                connId: connId,
                clientRequestedTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: CurrentTick);
        }

        /// <summary>
        /// Enqueue server-generated commands for a given tick.
        /// connId is always 0 for server-generated commands.
        /// </summary>
        public void EnqueueServerCommands(
            List<EngineCommand<EngineCommandType>> commands, int requestedTick = -1)
        {
            if (commands == null || commands.Count == 0)
                return;

            if (requestedTick == -1)
                requestedTick = CurrentTick + 1;

            _commandSystem.Enqueue(
                connId: 0,
                clientRequestedTick: requestedTick,
                clientCmdSeq: 0,
                commands: commands,
                currentServerTick: CurrentTick);
        }
    }
}
