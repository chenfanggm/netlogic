using Game;
using Sim.Commanding;
using Sim.Systems;

namespace Sim
{
    /// <summary>
    /// Pure authoritative simulation core.
    /// Owns World and is the ONLY authority allowed to mutate it.
    /// </summary>
    public sealed class ServerEngine
    {
        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;
        public long ServerTimeMs => _ticker.ServerTimeMs;
        public World ReadOnlyWorld => _world;

        private readonly TickTicker _ticker;
        private readonly World _world;
        private readonly CommandSystem _commandSystem;
        private readonly ISystemCommandSink[] _systems;

        public ServerEngine(int tickRateHz, World initialWorld)
        {
            _ticker = new TickTicker(tickRateHz);
            _world = initialWorld ?? throw new ArgumentNullException(nameof(initialWorld));

            MovementSystem movement = new MovementSystem();
            _systems = [
                movement
            ];
            _commandSystem = new CommandSystem(
                _systems,
                _ticker,
                maxFutureTicks: 2,
                maxPastTicks: 2,
                maxStoredTicks: 16);
        }

        public void EnqueueClientCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<ClientCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            _commandSystem.Enqueue(
                connId: connId,
                requestedClientTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: _ticker.CurrentTick);
        }

        public EngineTickResult TickOnce()
        {
            int tick = _ticker.Advance(1);

            // 1) Execute systems in stable order
            _commandSystem.Execute(tick, _world);
            // 2) World fixed step
            _world.Advance(1);

            return new EngineTickResult(
                serverTick: tick,
                serverTimeMs: _ticker.ServerTimeMs,
                snapshot: _world.BuildSnapshot(),
                reliableOps: []);
        }
    }
}
