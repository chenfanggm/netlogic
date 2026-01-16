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
        private readonly int _maxStoredTicks;

        private readonly ClientCommandBuffer _buffer;
        private readonly Dictionary<ClientCommandType, ISystemCommandSink> _routes =
            new Dictionary<ClientCommandType, ISystemCommandSink>(256);

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
        public void DispatchCommands(int tick)
        {
            foreach (int connId in _buffer.ConnectionIdsForTick(tick))
            {
                while (_buffer.TryDequeueForTick(tick, connId, out CommandBatch batch))
                {
                    foreach (ClientCommand cmd in batch.Commands)
                    {
                        if (_routes.TryGetValue(cmd.Type, out ISystemCommandSink? sink) && sink != null)
                            sink.EnqueueCommand(tick, connId, in cmd);
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
            DispatchCommands(tick);
            // 2) Execute systems in stable order
            for (int i = 0; i < _systems.Length; i++)
                _systems[i].Execute(tick, world);
            // 3) Cleanup central input buffer
            DropOldTick(tick);
        }

        /// <summary>
        /// Cleanup central buffer only.
        /// Systems do NOT store multi-tick data in Option-2.
        /// </summary>
        public void DropOldTick(int tick)
        {
            _buffer.DropOldTick(tick - _maxStoredTicks);
        }
    }
}
