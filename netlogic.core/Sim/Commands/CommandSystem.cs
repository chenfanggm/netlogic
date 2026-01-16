using Game;

namespace Sim.Commanding
{
    /// <summary>
    /// OPTION-2: Central command scheduling and dispatch.
    /// Owns central scheduling buffer and routing table.
    /// Dispatches commands into system inboxes (systems don't store multi-tick data).
    /// </summary>
    public sealed class CommandSystem
    {
        private ISystemCommandSink[] _systems;
        private readonly ClientCommandBuffer _buffer;
        private readonly Dictionary<ClientCommandType, ISystemCommandSink> _routes =
            new Dictionary<ClientCommandType, ISystemCommandSink>(256);

        private readonly int _maxStoredTicks;

        public CommandSystem(
            ISystemCommandSink[] systems,
            TickTicker ticker,
            int maxFutureTicks,
            int maxPastTicks,
            int maxStoredTicks)
        {
            if (systems == null || systems.Length == 0)
                throw new ArgumentException("systems must not be empty", nameof(systems));

            _systems = systems;
            _buffer = new ClientCommandBuffer(maxFutureTicks, maxPastTicks);
            _maxStoredTicks = maxStoredTicks;

            // Auto-register routes from system declarations
            RegisterRoutes(systems);
        }

        public void Enqueue(
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

        public void Dispatch(int tick)
        {
            foreach (int connId in _buffer.ConnectionIdsForTick(tick))
            {
                while (_buffer.TryDequeueForTick(tick, connId, out CommandBatch batch))
                {
                    foreach (ClientCommand cmd in batch.Commands)
                    {
                        if (_routes.TryGetValue(cmd.Type, out ISystemCommandSink? system) && system != null)
                            system.EnqueueCommand(tick, connId, in cmd);
                    }
                }
            }
        }

        /// <summary>
        /// Execute systems in stable order.
        /// </summary>
        public void Execute(int tick, World world)
        {
            // 1) Dispatch inputs for this tick into system inboxes
            Dispatch(tick);
            // 2) Execute systems in stable order
            for (int i = 0; i < _systems.Length; i++)
                _systems[i].Execute(tick, world);
            // 3) Cleanup central input buffer
            _buffer.DropOldTick(tick - _maxStoredTicks);

        }

        private void RegisterRoutes(ISystemCommandSink[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ISystemCommandSink sys = systems[i];
            }

            foreach (ISystemCommandSink system in systems)
            {
                IReadOnlyList<ClientCommandType> commandTypes = system.CommandTypes;
                if (commandTypes == null || commandTypes.Count == 0)
                    continue;

                foreach (ClientCommandType commandType in commandTypes)
                {
                    if (_routes.TryGetValue(commandType, out ISystemCommandSink? existing) &&
                        existing != null &&
                        !ReferenceEquals(existing, system))
                    {
                        throw new InvalidOperationException(
                            $"Command {commandType} owned by multiple systems: {existing.Name} and {system.Name}");
                    }

                    _routes[commandType] = system;
                }
            }
        }
    }
}
