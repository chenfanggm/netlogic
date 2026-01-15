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
        private readonly ClientCommandBuffer _buffer;

        private readonly Dictionary<ClientCommandType, ISystemCommandSink> _routes =
            new Dictionary<ClientCommandType, ISystemCommandSink>(256);

        private readonly ISystemCommandSink[] _systems;

        public CommandSystem(ISystemCommandSink[] systems, int maxFutureTicks = 2, int maxPastTicks = 2)
        {
            ArgumentNullException.ThrowIfNull(systems, $"{nameof(systems)} is null");

            if (systems.Length == 0)
                throw new ArgumentException("systems must not be empty", nameof(systems));

            _systems = systems;
            _buffer = new ClientCommandBuffer(maxFutureTicks, maxPastTicks);

            for (int i = 0; i < systems.Length; i++)
            {
                ISystemCommandSink sys = systems[i];
                ArgumentNullException.ThrowIfNull(sys, $"systems[{i}] is null");

                IReadOnlyList<ClientCommandType> owned = sys.OwnedCommandTypes;
                ArgumentNullException.ThrowIfNull(owned, $"systems[{i}].OwnedCommandTypes is null");

                for (int j = 0; j < owned.Count; j++)
                {
                    ClientCommandType type = owned[j];

                    if (_routes.TryGetValue(type, out ISystemCommandSink? existing) && existing != null && !ReferenceEquals(existing, sys))
                        throw new InvalidOperationException($"Command {type} owned by multiple systems: {existing.Name} and {sys.Name}");

                    _routes[type] = sys;
                }
            }
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
                while (_buffer.TryDequeueForTick(tick, connId, out CommandBatch batch))
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

            for (int i = 0; i < _systems.Length; i++)
                _systems[i].DropBeforeTick(oldestAllowedTick);
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
