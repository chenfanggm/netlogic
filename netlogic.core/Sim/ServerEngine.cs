using System;
using System.Collections.Generic;
using Game;
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
        private readonly ClientCommandBuffer2 _cmdBuffer;
        private readonly CommandRouter _router;
        private readonly IGameSystem[] _systems;

        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;
        public long ServerTimeMs => _ticker.ServerTimeMs;

        public ServerEngine(int tickRateHz, World initialWorld)
        {
            _world = initialWorld ?? throw new ArgumentNullException(nameof(initialWorld));
            _ticker = new TickTicker(tickRateHz);
            _cmdBuffer = new ClientCommandBuffer2();
            _router = new CommandRouter();

            _systems = new IGameSystem[]
            {
                new MovementSystem(),
            };
        }

        /// <summary>
        /// Read-only access for adapters (hashing, baselines, inspection).
        /// NEVER expose mutable World.
        /// </summary>
        public World ReadOnlyWorld => _world;

        public void EnqueueClientCommands(
            int connId,
            int clientTick,
            uint clientCmdSeq,
            List<ClientCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            _cmdBuffer.EnqueueWithValidation(
                connectionId: connId,
                clientTick: clientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                serverTick: _ticker.CurrentTick);
        }

        public EngineTickResult TickOnce()
        {
            _ticker.Advance(1);

            int tick = _ticker.CurrentTick;

            RouteCommandsForTick(tick);

            SystemInputs inputs = new SystemInputs(tick, _router);
            for (int i = 0; i < _systems.Length; i++)
                _systems[i].Execute(tick, ref _world, inputs);

            _world.StepFixed();

            _cmdBuffer.DropBeforeTick(tick - 16);
            _router.DropBeforeTick(tick - 16);

            return new EngineTickResult(
                serverTick: tick,
                serverTimeMs: _ticker.ServerTimeMs,
                snapshot: BuildSnapshot(),
                reliableOps: Array.Empty<EngineOpBatch>());
        }

        private void RouteCommandsForTick(int tick)
        {
            foreach (int connId in _cmdBuffer.ConnectionIdsForTick(tick))
            {
                while (_cmdBuffer.TryDequeueForTick(tick, connId, out ClientCommandBuffer2.CommandBatch commandBatch))
                {
                    _router.RouteBatch(commandBatch.ScheduledTick, connId, commandBatch.Commands);
                }
            }
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
