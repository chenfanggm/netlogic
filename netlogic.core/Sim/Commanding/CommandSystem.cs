using System;
using System.Collections.Generic;

namespace Sim.Commanding
{
    /// <summary>
    /// OPTION-2: Central command scheduling and dispatch.
    /// Owns central scheduling buffer and routing table.
    /// Dispatches commands into system inboxes (systems don't store multi-tick data).
    /// </summary>
    public sealed class CommandSystem
    {
        private readonly ClientCommandBuffer _buffer;

        private readonly Dictionary<ClientCommandType, ISystemCommandSink> _routes =
            new Dictionary<ClientCommandType, ISystemCommandSink>(256);

        private readonly ISystemCommandSink[] _systems;

        public CommandSystem(
            ISystemCommandSink[] systems,
            int maxFutureTicks,
            int maxPastTicks)
        {
            if (systems == null || systems.Length == 0)
                throw new ArgumentException("systems must not be empty", nameof(systems));

            _systems = systems;
            _buffer = new ClientCommandBuffer(maxFutureTicks, maxPastTicks);

            // Auto-register routes from system declarations
            for (int i = 0; i < systems.Length; i++)
            {
                ISystemCommandSink sys = systems[i];

                IReadOnlyList<ClientCommandType> owned = sys.OwnedCommandTypes;
                if (owned == null)
                    continue;

                for (int j = 0; j < owned.Count; j++)
                {
                    ClientCommandType type = owned[j];

                    if (_routes.TryGetValue(type, out ISystemCommandSink? existing) &&
                        existing != null &&
                        !ReferenceEquals(existing, sys))
                    {
                        throw new InvalidOperationException(
                            $"Command {type} owned by multiple systems: {existing.Name} and {sys.Name}");
                    }

                    _routes[type] = sys;
                }
            }
        }

        /// <summary>
        /// Stage incoming commands (called by ServerEngine / adapter).
        /// NO routing here.
        /// </summary>
        public void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<ClientCommand> commands,
            int currentServerTick)
        {
            if (commands == null || commands.Count == 0)
                return;

            _buffer.EnqueueWithValidation(
                connectionId: connId,
                requestedClientTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: currentServerTick);
        }

        /// <summary>
        /// OPTION-2 CORE:
        /// Dequeue all batches for tick and dispatch commands into system inboxes.
        /// Called ONLY from TickOnce().
        /// </summary>
        public void DispatchTick(int tick)
        {
            foreach (int connId in _buffer.ConnectionIdsForTick(tick))
            {
                while (_buffer.TryDequeueForTick(tick, connId, out CommandBatch batch))
                {
                    DispatchBatch(tick, connId, batch.Commands);
                }
            }
        }

        private void DispatchBatch(
            int tick,
            int connId,
            List<ClientCommand> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                ClientCommand cmd = commands[i];

                if (_routes.TryGetValue(cmd.Type, out ISystemCommandSink? sink) && sink != null)
                {
                    sink.EnqueueCommand(tick, connId, in cmd);
                }
            }
        }

        /// <summary>
        /// Cleanup central buffer only.
        /// Systems do NOT store multi-tick data in Option-2.
        /// </summary>
        public void DropOldTick(int oldestAllowedTick)
        {
            _buffer.DropOldTick(oldestAllowedTick);
        }
    }
}
