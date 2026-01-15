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
        private World _world;
        private readonly TickTicker _ticker;
        private readonly CommandSystem _commandSystem;
        private readonly ISystemCommandSink[] _systems;

        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;
        public long ServerTimeMs => _ticker.ServerTimeMs;

        public ServerEngine(int tickRateHz, World initialWorld)
        {
            _world = initialWorld ?? throw new ArgumentNullException(nameof(initialWorld));
            _ticker = new TickTicker(tickRateHz);

            MovementSystem movement = new MovementSystem();
            _systems = [
                movement
            ];
            _commandSystem = new CommandSystem(_systems, maxFutureTicks: 2, maxPastTicks: 2, maxStoredTicks: 16);
        }

        /// <summary>
        /// Read-only access for adapters (hashing, baselines, inspection).
        /// NEVER expose mutable World.
        /// </summary>
        public World ReadOnlyWorld => _world;

        public void EnqueueClientCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<ClientCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            _commandSystem.EnqueueCommands(
                connId: connId,
                requestedClientTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: _ticker.CurrentTick);
        }

        public EngineTickResult TickOnce()
        {
            int tick = _ticker.Advance(1);

            // 1) Dispatch inputs for this tick into system inboxes
            _commandSystem.DispatchCommands(tick);

            // 2) Execute systems in stable order
            foreach (ISystemCommandSink system in _systems)
                system.Execute(tick, ref _world);

            // 3) World fixed step
            _world.Advance(1);

            // 4) Cleanup central input buffer
            _commandSystem.DropOldTick(tick);

            return new EngineTickResult(
                serverTick: tick,
                serverTimeMs: _ticker.ServerTimeMs,
                snapshot: _world.BuildSnapshot(),
                reliableOps: []);
        }
    }
}
