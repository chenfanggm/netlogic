// FILE: netlogic.core/Sim/Systems/SystemBase.cs
// Base system with handler registry and tick-local inbox.

using System;
using System.Collections.Generic;
using com.aqua.netlogic.command;
using com.aqua.netlogic.command.handler;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.command.sink
{
    public abstract class CommandSinkBase<TCommandType> : ICommandSink<TCommandType>
        where TCommandType : struct, Enum
    {
        /// <summary>
        /// Command types owned by this system (used by CommandSystem routing).
        /// </summary>
        public IReadOnlyList<TCommandType> CommandTypes => _ownedCmdTypes;

        private readonly TCommandType[] _ownedCmdTypes;
        private readonly Dictionary<TCommandType, IEngineCommandHandler<TCommandType>> _handlers;
        private readonly List<EngineCommand<TCommandType>> _inbox;

        protected CommandSinkBase(
            IEnumerable<IEngineCommandHandler<TCommandType>> handlers,
            int inboxCapacity = 256,
            int handlerCapacity = 16)
        {
            ArgumentNullException.ThrowIfNull(handlers);

            _handlers = new Dictionary<TCommandType, IEngineCommandHandler<TCommandType>>(handlerCapacity);
            _inbox = new List<EngineCommand<TCommandType>>(inboxCapacity);

            List<TCommandType> ownedCmdTypes = new List<TCommandType>(handlerCapacity);

            foreach (IEngineCommandHandler<TCommandType> handler in handlers)
            {
                RegisterHandler(handler);
                ownedCmdTypes.Add(handler.CommandType);
            }

            ownedCmdTypes.Sort(Comparer<TCommandType>.Default.Compare);
            _ownedCmdTypes = [.. ownedCmdTypes];
        }

        protected void RegisterHandler(IEngineCommandHandler<TCommandType> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);

            TCommandType type = handler.CommandType;

            if (_handlers.TryGetValue(type, out IEngineCommandHandler<TCommandType>? existing) && existing != handler)
                throw new InvalidOperationException(
                    $"Duplicate handler for {type} in system {GetType().Name}: {existing.GetType().Name} vs {handler.GetType().Name}");

            _handlers[type] = handler;
        }

        public void InboxCommand(EngineCommand<TCommandType> command)
        {
            if (command == null)
                return;

            _inbox.Add(command);
        }

        /// <summary>
        /// Called by ServerEngine in stable order each tick.
        /// </summary>
        public void Execute(com.aqua.netlogic.sim.game.ServerModel world, OpWriter ops)
        {
            for (int i = 0; i < _inbox.Count; i++)
            {
                EngineCommand<TCommandType> command = _inbox[i];
                if (_handlers.TryGetValue(command.Type, out IEngineCommandHandler<TCommandType>? handler) && handler != null)
                    handler.Handle(world, ops, command);
                else
                    throw new InvalidOperationException($"No handler found for {command.Type} in system {GetType().Name}");
            }

            _inbox.Clear();

            ExecuteAfterCommands(world, ops);
        }

        /// <summary>
        /// If you want per-system additional logic before/after handler execute, do it here.
        /// Most systems can simply rely on command-driven execution.
        /// </summary>
        protected virtual void ExecuteAfterCommands(com.aqua.netlogic.sim.game.ServerModel world, OpWriter ops) { }

        [Obsolete("Reflection-based handler discovery is disabled. Use EngineCommandHandlerRegistry and explicit registration.", error: true)]
        protected static IEngineCommandHandler<TCommandType>[] DiscoverHandlersForSystem(Type systemType)
        {
            throw new NotSupportedException("Reflection-based handler discovery is disabled. Use EngineCommandHandlerRegistry.");
        }
    }
}
