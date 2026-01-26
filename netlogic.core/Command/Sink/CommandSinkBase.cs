// FILE: netlogic.core/Sim/Systems/SystemBase.cs
// Base system with handler registry and tick-local inbox.
using System.Reflection;

namespace Sim.Command
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
        public void Execute(Game.Game world)
        {
            for (int i = 0; i < _inbox.Count; i++)
            {
                EngineCommand<TCommandType> command = _inbox[i];
                if (_handlers.TryGetValue(command.Type, out IEngineCommandHandler<TCommandType>? handler) && handler != null)
                    handler.Handle(world, command);
                else
                    throw new InvalidOperationException($"No handler found for {command.Type} in system {GetType().Name}");
            }

            _inbox.Clear();

            ExecuteAfterCommands(world);
        }

        /// <summary>
        /// If you want per-system additional logic before/after handler execute, do it here.
        /// Most systems can simply rely on command-driven execution.
        /// </summary>
        protected virtual void ExecuteAfterCommands(Game.Game world) { }

        protected static IEngineCommandHandler<TCommandType>[] DiscoverHandlersForSystem(Type systemType)
        {
            ArgumentNullException.ThrowIfNull(systemType);

            List<IEngineCommandHandler<TCommandType>> list = new List<IEngineCommandHandler<TCommandType>>(16);

            Type handlerInterface = typeof(IEngineCommandHandler<TCommandType>);

            Type[] types;
            try
            {
                types = Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type? type = types[i];
                if (type == null) continue;

                if (!type.IsClass || type.IsAbstract) continue;
                if (!handlerInterface.IsAssignableFrom(type)) continue;

                object[] attrs = type.GetCustomAttributes(typeof(EngineCommandHandlerAttribute), inherit: false);
                bool belongs = false;
                for (int a = 0; a < attrs.Length; a++)
                {
                    EngineCommandHandlerAttribute attr = (EngineCommandHandlerAttribute)attrs[a];
                    if (attr.SystemType == systemType)
                    {
                        belongs = true;
                        break;
                    }
                }
                if (!belongs) continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"Handler {type.FullName} is marked for {systemType.Name} but has no parameterless constructor.");
                }

                IEngineCommandHandler<TCommandType> instance =
                    (IEngineCommandHandler<TCommandType>)Activator.CreateInstance(type)!;
                list.Add(instance);
            }

            list.Sort((x, y) => Comparer<TCommandType>.Default.Compare(x.CommandType, y.CommandType));

            return [.. list];
        }
    }
}
