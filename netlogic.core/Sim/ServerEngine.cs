using System;
using System.Collections.Generic;
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

            _systems = new ISystemCommandSink[]
            {
                movement,
            };

            _commandSystem = new CommandSystem();
            _commandSystem.RegisterSystem(movement);
            _commandSystem.MapMany(movement, ClientCommandType.MoveBy);
            _commandSystem.MaxFutureTicks = 2;
            _commandSystem.MaxPastTicks = 2;
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

            _commandSystem.EnqueueClientBatch(
                connId: connId,
                requestedClientTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: _ticker.CurrentTick);
        }

        public EngineTickResult TickOnce()
        {
            _ticker.Advance(1);

            int tick = _ticker.CurrentTick;

            _commandSystem.RouteTick(tick);

            for (int i = 0; i < _systems.Length; i++)
                _systems[i].Execute(tick, ref _world);

            _world.StepFixed();

            _commandSystem.DropBeforeTick(tick - 16);

            return new EngineTickResult(
                serverTick: tick,
                serverTimeMs: _ticker.ServerTimeMs,
                snapshot: BuildSnapshot(),
                reliableOps: Array.Empty<EngineOpBatch>());
        }

        private SampleEntityPos[] BuildSnapshot()
        {
            List<SampleEntityPos> list = new List<SampleEntityPos>(128);
            foreach (Entity e in _world.Entities)
                list.Add(new SampleEntityPos(e.Id, e.X, e.Y));
            return list.ToArray();
        }
    }
}
