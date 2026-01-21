using System;
using System.Collections.Generic;
using Game;

namespace Sim.Commanding
{
    /// <summary>
    /// Central command scheduling and dispatch.
    /// Owns central scheduling buffer and routing table.
    /// Dispatches commands into sink inboxes (sinks don't store multi-tick data).
    /// </summary>
    public sealed class CommandSystem<TCommandType>
        where TCommandType : struct, Enum
    {
        private ICommandSink<TCommandType>[] _sinks;
        private readonly EngineCommandBuffer<TCommandType> _buffer;
        private readonly Dictionary<TCommandType, ICommandSink<TCommandType>> _routes =
            new Dictionary<TCommandType, ICommandSink<TCommandType>>(256);

        public CommandSystem(
            ICommandSink<TCommandType>[] sinks,
            int maxFutureTicks,
            int maxPastTicks,
            int maxStoredTicks)
        {
            _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
            _buffer = new EngineCommandBuffer<TCommandType>(maxFutureTicks, maxPastTicks, maxStoredTicks);
            // Auto-register routes from system declarations
            RegisterRoutes(sinks);
        }

        public void Enqueue(
            int connId,
            int clientRequestedTick,
            uint clientCmdSeq,
            List<EngineCommand<TCommandType>> commands,
            int currentServerTick)
        {
            if (commands == null || commands.Count == 0)
                return;

            _buffer.EnqueueWithValidation(
                connId: connId,
                clientRequestedTick: clientRequestedTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: currentServerTick);
        }

        /// <summary>
        /// Execute sinks in stable order.
        /// </summary>
        public void Execute(int tick, World world)
        {
            // 1) Dispatch inputs for this tick into sink inboxes
            Dispatch(tick);
            // 2) Execute sinks in stable order
            for (int i = 0; i < _sinks.Length; i++)
                _sinks[i].Execute(world);
            // 3) Cleanup central input buffer
            _buffer.DropOldTick(tick);

        }

        private void Dispatch(int tick)
        {
            foreach (int connId in _buffer.GetConnIdsByTick(tick))
            {
                while (_buffer.TryDequeueForTick(tick, connId, out EngineCommandBatch<TCommandType> batch))
                {
                    foreach (EngineCommand<TCommandType> cmd in batch.Commands)
                    {
                        if (_routes.TryGetValue(cmd.Type, out ICommandSink<TCommandType>? sink) && sink != null)
                            sink.InboxCommand(cmd);
                    }
                }
            }
        }

        private void RegisterRoutes(ICommandSink<TCommandType>[] sinks)
        {
            foreach (ICommandSink<TCommandType> sink in sinks)
            {
                IReadOnlyList<TCommandType> commandTypes = sink.CommandTypes;
                if (commandTypes == null || commandTypes.Count == 0)
                    continue;

                foreach (TCommandType commandType in commandTypes)
                {
                    if (_routes.TryGetValue(commandType, out ICommandSink<TCommandType>? existing))
                    {
                        throw new InvalidOperationException(
                            $"Command {commandType} owned by multiple sinks: {existing.GetType().Name} and {sink.GetType().Name}");
                    }

                    _routes[commandType] = sink;
                }
            }
        }
    }
}
