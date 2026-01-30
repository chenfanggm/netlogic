using System;
using System.Collections.Generic;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.command.buffer;
using com.aqua.netlogic.command.sink;


namespace com.aqua.netlogic.command
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
            _buffer = new EngineCommandBuffer<TCommandType>(
                maxPastTicks: maxPastTicks,
                maxFutureTicks: maxFutureTicks,
                lateGraceTicks: 1,
                maxStoredTicks: maxStoredTicks);
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
                currentServerTick: currentServerTick,
                connId: connId,
                clientRequestedTick: clientRequestedTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands);
        }

        public void GetCommandBufferStats(
            out long droppedTooOld,
            out long snappedLate,
            out long clampedFuture,
            out long accepted)
        {
            droppedTooOld = _buffer.DroppedTooOldCount;
            snappedLate = _buffer.SnappedLateCount;
            clampedFuture = _buffer.ClampedFutureCount;
            accepted = _buffer.AcceptedCount;
        }

        /// <summary>
        /// Execute sinks in stable order.
        /// </summary>
        public void Execute(int tick, com.aqua.netlogic.sim.game.ServerModel world)
        {
            // 1) Dispatch inputs for this tick into sink inboxes (with phases)
            Dispatch(tick);
            // 2) Execute sinks in stable order
            for (int i = 0; i < _sinks.Length; i++)
                _sinks[i].Execute(world);
        }

        private void Dispatch(int tick)
        {
            List<int> connIds = [.. _buffer.GetConnIdsByTick(tick)];
            connIds.Sort();

            foreach (int connId in connIds)
            {
                while (_buffer.TryDequeueForTick(tick, connId, out EngineCommandBatch<TCommandType> batch))
                {
                    List<EngineCommand<TCommandType>> cmds = batch.Commands;
                    if (cmds == null || cmds.Count == 0)
                        continue;

                    // One-pass stable dispatch:
                    // - Priority 0 inboxed immediately
                    // - Non-zero queued and inboxed after (preserves phase semantics)
                    List<EngineCommand<TCommandType>>? nonZero = null;

                    for (int i = 0; i < cmds.Count; i++)
                    {
                        EngineCommand<TCommandType> cmd = cmds[i];
                        int prio = _priorityOfType(cmd.Type);

                        if (!_routes.TryGetValue(cmd.Type, out ICommandSink<TCommandType>? sink) || sink == null)
                            continue;

                        if (prio == 0)
                        {
                            sink.InboxCommand(cmd);
                        }
                        else
                        {
                            nonZero ??= new List<EngineCommand<TCommandType>>(8);
                            nonZero.Add(cmd);
                        }
                    }

                    if (nonZero != null)
                    {
                        for (int i = 0; i < nonZero.Count; i++)
                        {
                            EngineCommand<TCommandType> cmd = nonZero[i];
                            if (_routes.TryGetValue(cmd.Type, out ICommandSink<TCommandType>? sink) && sink != null)
                                sink.InboxCommand(cmd);
                        }
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
