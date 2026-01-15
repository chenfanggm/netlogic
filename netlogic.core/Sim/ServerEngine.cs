using System;
using System.Collections.Generic;
using Game;

namespace Sim
{
    /// <summary>
    /// Pure authoritative simulation core.
    /// - No transport
    /// - No protocol encoding
    /// - No lanes
    /// - No hashing
    /// - World mutates ONLY in TickOnce()
    /// </summary>
    public sealed class ServerEngine
    {
        private readonly World _world;
        private readonly TickTicker _ticker;
        private readonly ClientCommandBuffer2 _cmdBuffer;

        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;
        public long ServerTimeMs => _ticker.ServerTimeMs;

        public ServerEngine(int tickRateHz, World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _ticker = new TickTicker(tickRateHz);
            _cmdBuffer = new ClientCommandBuffer2();
        }

        /// <summary>
        /// Buffer an incoming batch of commands. Never executes here.
        /// Executed only inside TickOnce().
        /// </summary>
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

            _cmdBuffer.EnqueueWithValidation(
                connectionId: connId,
                requestedClientTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: trimmed,
                currentServerTick: _ticker.CurrentTick,
                scheduledTick: out _);
        }

        /// <summary>
        /// Advances authoritative simulation by one fixed tick and returns engine-level results.
        /// Adapter is responsible for hashing + packetization.
        /// </summary>
        public EngineTickResult TickOnce()
        {
            _ticker.Advance(1);

            ExecuteCommandsForCurrentTick();
            _cmdBuffer.DropBeforeTick(_ticker.CurrentTick - 16);

            // TODO: authoritative systems here (AI/combat/collisions/etc) — deterministic only
            _world.StepFixed();

            // Continuous snapshot (for Sample lane encoding by adapter)
            SampleEntityPos[] snapshot = BuildSnapshot();

            // Discrete reliable ops (domain-level); currently none in this demo
            EngineOpBatch[] reliableOps = Array.Empty<EngineOpBatch>();

            return new EngineTickResult(
                serverTick: _ticker.CurrentTick,
                serverTimeMs: _ticker.ServerTimeMs,
                snapshot: snapshot,
                reliableOps: reliableOps);
        }

        // -------------------------
        // Apply commands (deterministic)
        // -------------------------

        private void ExecuteCommandsForCurrentTick()
        {
            int tick = _ticker.CurrentTick;

            foreach (int connId in _cmdBuffer.ConnectionIdsForTick(tick))
            {
                while (_cmdBuffer.TryDequeueForTick(tick, connId, out ClientCommandBuffer2.ScheduledBatch batch))
                {
                    ApplyClientCommands(batch.Commands);
                }
            }
        }

        private void ApplyClientCommands(ClientCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
                return;

            int i = 0;
            while (i < commands.Length)
            {
                ClientCommand c = commands[i];

                if (c.Type == ClientCommandType.MoveBy)
                    _world.TryMoveEntityBy(c.EntityId, c.Dx, c.Dy);

                i++;
            }
        }

        // -------------------------
        // Snapshot builder
        // -------------------------

        private SampleEntityPos[] BuildSnapshot()
        {
            List<SampleEntityPos> list = new List<SampleEntityPos>(128);

            foreach (Entity e in _world.Entities)
                list.Add(new SampleEntityPos(e.Id, e.X, e.Y));

            return list.ToArray();
        }

        private static ClientCommand[] Trim(ClientCommand[] src, int count)
        {
            if (count <= 0)
                return Array.Empty<ClientCommand>();

            if (src.Length == count)
                return src;

            ClientCommand[] dst = new ClientCommand[count];

            int i = 0;
            while (i < count)
            {
                dst[i] = src[i];
                i++;
            }

            return dst;
        }
    }
}
