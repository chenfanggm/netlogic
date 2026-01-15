using System.Linq;

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
        private readonly ISystemCommandSink[] _systems;
        private readonly ClientCommandBuffer _buffer;

        private readonly Dictionary<ClientCommandType, ISystemCommandSink> _routes =
            new Dictionary<ClientCommandType, ISystemCommandSink>(256);


        public CommandSystem(ISystemCommandSink[] systems, int maxFutureTicks = 2, int maxPastTicks = 2)
        {
            _systems = systems ?? throw new ArgumentNullException(nameof(systems), "systems must not be null");
            _buffer = new ClientCommandBuffer(maxFutureTicks, maxPastTicks);

            foreach (ISystemCommandSink sys in _systems)
            {
                IReadOnlyList<ClientCommandType> cmdTypes = sys.OwnedCommandTypes;
                foreach (ClientCommandType item in cmdTypes)
                    if (!_routes.TryAdd(item, sys))
                        throw new InvalidOperationException($"Command {item} owned by multiple systems: {sys.Name}");
            }
        }

        /// <summary>
        /// CommandSystem schedules commands for execution on a server tick (validation inside buffer).
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
