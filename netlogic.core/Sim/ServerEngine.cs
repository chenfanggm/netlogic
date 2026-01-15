using System;
using System.Collections.Generic;
using Game;
using Sim.Commands;

namespace Sim
{
    /// <summary>
    /// Pure authoritative simulation core.
    /// Owns World and is the ONLY authority allowed to mutate it.
    /// </summary>
    public sealed class ServerEngine
    {
        private readonly TickTicker _ticker;
        private readonly World _world;
        private readonly ClientCommandBuffer2 _cmdBuffer;
        private readonly ClientCommandHandlerRegistry _handlers;

        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;
        public long ServerTimeMs => _ticker.ServerTimeMs;

        public ServerEngine(int tickRateHz, World initialWorld)
        {
            _ticker = new TickTicker(tickRateHz);
            _world = initialWorld ?? throw new ArgumentNullException(nameof(initialWorld));
            _cmdBuffer = new ClientCommandBuffer2();
            _handlers = new ClientCommandHandlerRegistry();
            _handlers.RegisterMany(
                new MoveByCommandHandler());
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

            ExecuteCommandsForCurrentTick();
            _cmdBuffer.DropBeforeTick(_ticker.CurrentTick - 16);

            _world.StepFixed();

            return new EngineTickResult(
                serverTick: _ticker.CurrentTick,
                serverTimeMs: _ticker.ServerTimeMs,
                snapshot: BuildSnapshot(),
                reliableOps: Array.Empty<EngineOpBatch>());
        }

        private void ExecuteCommandsForCurrentTick()
        {
            int tick = _ticker.CurrentTick;

            foreach (int connId in _cmdBuffer.ConnectionIdsForTick(tick))
            {
                while (_cmdBuffer.TryDequeueForTick(tick, connId, out ClientCommandBuffer2.CommandBatch commandBatch))
                {
                    _handlers.ApplyAll(_world, commandBatch.Commands);
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
