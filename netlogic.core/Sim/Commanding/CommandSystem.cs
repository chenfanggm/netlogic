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
            new Dictionary<ClientCommandType, ISystemCommandSink>(256);

        private ISystemCommandSink[] _systems = Array.Empty<ISystemCommandSink>();

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

        /// <summary>
        /// Initializes command routing using systems' OwnedCommandTypes declarations.
        /// Must be called once at startup.
        /// </summary>
        public void Initialize(ISystemCommandSink[] systems)
        {
            if (systems == null)
                throw new ArgumentNullException(nameof(systems));

            if (systems.Length == 0)
                throw new ArgumentException("systems must not be empty", nameof(systems));

            _systems = systems;
            _routes.Clear();

            for (int i = 0; i < systems.Length; i++)
            {
                ISystemCommandSink sys = systems[i];
                if (sys == null)
                    throw new ArgumentException("systems contains null", nameof(systems));

                IReadOnlyList<ClientCommandType> owned = sys.OwnedCommandTypes;
                if (owned == null)
                    continue;

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
