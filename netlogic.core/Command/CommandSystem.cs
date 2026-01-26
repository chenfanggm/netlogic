using System;
using System.Collections.Generic;
using Sim.Game;


namespace Sim.Command
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
        /// <summary>
        /// Priority/phase mapping for dispatch.
        /// Lower value dispatches earlier.
        /// Default: 0 for all types (single phase).
        /// </summary>
        private readonly Func<TCommandType, int> _priorityOfType;

        public CommandSystem(
            ICommandSink<TCommandType>[] sinks,
            int maxFutureTicks,
            int maxPastTicks,
            int maxStoredTicks,
            Func<TCommandType, int>? priorityOfType = null)
        {
            _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
            _buffer = new EngineCommandBuffer<TCommandType>(maxFutureTicks, maxPastTicks, maxStoredTicks);
            _priorityOfType = priorityOfType ?? (_ => 0);
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
        public void Execute(int tick, Game.Game world)
        {
            // 1) Dispatch inputs for this tick into sink inboxes (with phases)
            Dispatch(tick);
            // 2) Execute sinks in stable order
            for (int i = 0; i < _sinks.Length; i++)
                _sinks[i].Execute(world);
            // 3) Cleanup central input buffer
            _buffer.DropOldTick(tick);

        }

        private void Dispatch(int tick)
        {
            // BUGFIX: Ensure deterministic connId iteration order.
            // If GetConnIdsByTick returns a HashSet/Dictionary-backed enumeration,
            // foreach order is not guaranteed across runs/platforms.
            List<int> connIds = [.. _buffer.GetConnIdsByTick(tick)];
            connIds.Sort();

            foreach (int connId in connIds)
            {
                while (_buffer.TryDequeueForTick(tick, connId, out EngineCommandBatch<TCommandType> batch))
                {
                    List<EngineCommand<TCommandType>> cmds = batch.Commands;
                    if (cmds == null || cmds.Count == 0)
                        continue;

                    // Phase 0: priority == 0 (typically: FlowState transitions)
                    DispatchPhase(cmds, desiredPriority: 0);

                    // Phase 1+: everything else
                    DispatchNonZero(cmds);
                }
            }
        }

        private void DispatchPhase(List<EngineCommand<TCommandType>> cmds, int desiredPriority)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                EngineCommand<TCommandType> cmd = cmds[i];
                if (_priorityOfType(cmd.Type) != desiredPriority)
                    continue;

                if (_routes.TryGetValue(cmd.Type, out ICommandSink<TCommandType>? sink) && sink != null)
                    sink.InboxCommand(cmd);
            }
        }

        private void DispatchNonZero(List<EngineCommand<TCommandType>> cmds)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                EngineCommand<TCommandType> cmd = cmds[i];
                if (_priorityOfType(cmd.Type) == 0)
                    continue;

                if (_routes.TryGetValue(cmd.Type, out ICommandSink<TCommandType>? sink) && sink != null)
                    sink.InboxCommand(cmd);
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
