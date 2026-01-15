using System;
using System.Collections.Generic;

namespace Sim.Commanding
{
    /// <summary>
    /// Owns:
    /// - tick scheduling buffer (ClientCommandBuffer2)
    /// - routing registry (ClientCommandType -> ISystemCommandSink)
    /// - per-tick routing execution (dequeue batches, route to systems)
    ///
    /// Does NOT mutate World directly.
    /// Systems mutate World in their Execute() in stable order.
    /// </summary>
    public sealed class CommandSystem
    {
        private readonly ClientCommandBuffer2 _buffer = new ClientCommandBuffer2();

        private readonly Dictionary<ClientCommandType, ISystemCommandSink> _routes =
            new Dictionary<ClientCommandType, ISystemCommandSink>(128);

        private readonly Dictionary<string, ISystemCommandSink> _systemsByName =
            new Dictionary<string, ISystemCommandSink>(32);

        public int MaxFutureTicks
        {
            get => _buffer.MaxFutureTicks;
            set => _buffer.MaxFutureTicks = value;
        }

        public int MaxPastTicks
        {
            get => _buffer.MaxPastTicks;
            set => _buffer.MaxPastTicks = value;
        }

        public void RegisterSystem(ISystemCommandSink system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (_systemsByName.ContainsKey(system.Name))
                throw new InvalidOperationException($"Duplicate system name: {system.Name}");

            _systemsByName.Add(system.Name, system);
        }

        public void Map(ClientCommandType type, ISystemCommandSink system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (_routes.TryGetValue(type, out ISystemCommandSink? existing) && existing != null && !ReferenceEquals(existing, system))
                throw new InvalidOperationException($"Command {type} already mapped to {existing.Name}");

            _routes[type] = system;
        }

        public void MapMany(ISystemCommandSink system, params ClientCommandType[] types)
        {
            for (int i = 0; i < types.Length; i++)
                Map(types[i], system);
        }

        /// <summary>
        /// Adapter calls this with a fresh list (no reuse).
        /// CommandSystem schedules it for execution on a server tick (validation inside buffer).
        /// </summary>
        public void EnqueueClientBatch(
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
                currentServerTick: currentServerTick,
                scheduledTick: out _);
        }

        /// <summary>
        /// Routes all command batches scheduled for this tick into the mapped systems' queues.
        /// </summary>
        public void RouteTick(int tick)
        {
            foreach (int connId in _buffer.ConnectionIdsForTick(tick))
            {
                while (_buffer.TryDequeueForTick(tick, connId, out ClientCommandBuffer2.CommandBatch batch))
                {
                    RouteBatch(tick, connId, batch.Commands);
                }
            }
        }

        /// <summary>
        /// Cleanup old buffered batches (engine-owned tick policy).
        /// </summary>
        public void DropBeforeTick(int oldestAllowedTick)
        {
            _buffer.DropBeforeTick(oldestAllowedTick);
        }

        private void RouteBatch(int scheduledTick, int connId, List<ClientCommand> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                ClientCommand c = commands[i];

                if (_routes.TryGetValue(c.Type, out ISystemCommandSink? sink) && sink != null)
                {
                    sink.EnqueueCommand(scheduledTick, connId, in c);
                    continue;
                }
            }
        }
    }
}
