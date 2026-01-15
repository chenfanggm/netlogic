using System;
using System.Collections.Generic;
using Game;
using Net;

namespace Sim
{
    /// <summary>
    /// Pure authoritative simulation core.
    /// - No transport
    /// - No protocol
    /// - World mutates ONLY in TickOnce()
    /// </summary>
    public sealed class ServerEngine(int tickRateHz, World world)
    {
        private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
        private readonly TickTicker _ticker = new TickTicker(tickRateHz);
        private readonly ClientCommandBuffer _cmdBuffer = new ClientCommandBuffer();

        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;
        public long ServerTimeMs => _ticker.ServerTimeMs;

        public void EnqueueClientCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            ClientCommand[] commands,
            int commandCount)
        {
            if (commands == null || commandCount <= 0)
                return;

            ClientCommand[] trimmed = Trim(commands, commandCount);

            _ = _cmdBuffer.EnqueueWithValidation(
                connectionId: connId,
                requestedClientTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: trimmed,
                currentServerTick: _ticker.CurrentTick,
                scheduledTick: out _);
        }

        /// <summary>
        /// Advances the simulation by exactly one fixed tick and returns the authoritative result.
        /// </summary>
        public EngineTickResult TickOnce()
        {
            _ticker.Advance(1);

            ExecuteCommandsForCurrentTick();
            _cmdBuffer.DropBeforeTick(_ticker.CurrentTick - 16);

            _world.StepFixed();

            SampleEntityPos[] sample = BuildSamplePositions();
            uint worldHash = StateHash.ComputeWorldHash(_world);

            return new EngineTickResult(
                serverTick: _ticker.CurrentTick,
                serverTimeMs: _ticker.ServerTimeMs,
                worldHash: worldHash,
                samplePositions: sample,
                reliableOps: Array.Empty<EngineReliableOpBatch>());
        }

        private void ExecuteCommandsForCurrentTick()
        {
            int tick = _ticker.CurrentTick;

            foreach (int connId in _cmdBuffer.ConnectionIdsForTick(tick))
            {
                while (_cmdBuffer.TryDequeueForTick(tick, connId, out ClientCommandBuffer.ScheduledBatch batch))
                {
                    ApplyClientCommands(batch.Commands);
                }
            }
        }

        private void ApplyClientCommands(ClientCommand[] commands)
        {
            int i = 0;
            while (i < commands.Length)
            {
                ClientCommand c = commands[i];

                if (c.Type == ClientCommandType.MoveBy)
                    _world.TryMoveEntityBy(c.EntityId, c.Dx, c.Dy);

                i++;
            }
        }

        private SampleEntityPos[] BuildSamplePositions()
        {
            List<SampleEntityPos> list = new List<SampleEntityPos>(128);
            foreach (Entity e in _world.Entities)
                list.Add(new SampleEntityPos(e.Id, e.X, e.Y));

            return list.ToArray();
        }

        private static ClientCommand[] Trim(ClientCommand[] src, int count)
        {
            if (src.Length == count)
                return src;

            ClientCommand[] dst = new ClientCommand[count];
            Array.Copy(src, dst, count);
            return dst;
        }
    }
}
