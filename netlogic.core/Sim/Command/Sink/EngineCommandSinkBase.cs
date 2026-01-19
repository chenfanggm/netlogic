// FILE: netlogic.core/Sim/Systems/SystemBase.cs
// Base system with handler registry and tick-local inbox.

using System.Reflection;
using Game;
using Sim.Commanding;

namespace Sim.Systems
{
    public abstract class EngineCommandSinkBase : IEngineCommandSink
    {
        /// <summary>
        /// Command types owned by this system (used by CommandSystem routing).
        /// Typically this should match the handlers you register.
        /// </summary>
        public IReadOnlyList<EngineCommandType> CommandTypes => _ownedCmdTypes;

        private readonly EngineCommandType[] _ownedCmdTypes;
        private readonly Dictionary<EngineCommandType, IEngineCommandHandler> _handlers;
        private readonly List<EngineCommand> _inbox;

        protected EngineCommandSinkBase(
            IEnumerable<IEngineCommandHandler> handlers,
            int inboxCapacity = 256,
            int handlerCapacity = 16)
        {
            ArgumentNullException.ThrowIfNull(handlers);

            _handlers = new Dictionary<EngineCommandType, IEngineCommandHandler>(handlerCapacity);
            _inbox = new List<EngineCommand>(inboxCapacity);

            List<EngineCommandType> ownedCmdTypes = new List<EngineCommandType>(handlerCapacity);

            foreach (IEngineCommandHandler handler in handlers)
            {
                RegisterHandler(handler);
                ownedCmdTypes.Add(handler.CommandType);
            }

            ownedCmdTypes.Sort((a, b) => ((byte)a).CompareTo((byte)b));
            _ownedCmdTypes = [.. ownedCmdTypes];
        }

        protected void RegisterHandler(IEngineCommandHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);

            EngineCommandType type = handler.CommandType;

            if (_handlers.TryGetValue(type, out IEngineCommandHandler? existing) && existing != handler)
                throw new InvalidOperationException(
                    $"Duplicate handler for {type} in system {GetType().Name}: {existing.GetType().Name} vs {handler.GetType().Name}");

            _handlers[type] = handler;
        }

        public void InboxCommand(EngineCommand command)
        {
            if (command == null)
                return;

            _inbox.Add(command);
        }

        /// <summary>
        /// Called by ServerEngine in stable order each tick.
        /// Sink should consume its tick-local inbox here.
        /// </summary>
        public void Execute(World world)
        {
            for (int i = 0; i < _inbox.Count; i++)
            {
                EngineCommand command = _inbox[i];
                if (_handlers.TryGetValue(command.Type, out IEngineCommandHandler? handler) && handler != null)
                    handler.Handle(world, command);
            }

            _inbox.Clear();

            ExecuteAfterCommands(world);
        }

        /// <summary>
        /// If you want per-system additional logic before/after handler execute, do it here.
        /// Most systems can simply rely on command-driven execution.
        /// </summary>
        protected virtual void ExecuteAfterCommands(World world) { }

        protected static IEngineCommandHandler[] DiscoverHandlersForSystem(Type systemType)
        {
            ArgumentNullException.ThrowIfNull(systemType);


            List<IEngineCommandHandler> list = new List<IEngineCommandHandler>(16);

            Type handlerInterface = typeof(IEngineCommandHandler);

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

                IEngineCommandHandler instance = (IEngineCommandHandler)Activator.CreateInstance(type)!;
                list.Add(instance);
            }

            list.Sort((x, y) => ((byte)x.CommandType).CompareTo((byte)y.CommandType));

            return [.. list];
        }
    }
}
