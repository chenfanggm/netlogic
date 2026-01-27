using System;
using Sim.Game;

namespace Sim.Command
{
    /// <summary>
    /// Explicit, reflection-free registry for mapping systems to their command handlers.
    ///
    /// Professional rule:
    /// - When you add a new handler, you MUST register it here.
    /// - No runtime type scanning. No Activator.CreateInstance.
    /// </summary>
    internal static class EngineCommandHandlerRegistry
    {
        public static IEngineCommandHandler<EngineCommandType>[] ForGameFlowSystem()
        {
            // Register GameFlowSystem handlers here.
            return new IEngineCommandHandler<EngineCommandType>[]
            {
                new FlowIntentHandler(),
            };
        }

        public static IEngineCommandHandler<EngineCommandType>[] ForMovementSystem()
        {
            // Register MovementSystem handlers here.
            return new IEngineCommandHandler<EngineCommandType>[]
            {
                new MoveByHandler(),
            };
        }

        /// <summary>
        /// Optional guard: call this at startup if you want to catch duplicate registrations early.
        /// </summary>
        public static void ValidateNoDuplicateCommandTypes(IEngineCommandHandler<EngineCommandType>[] handlers, Type systemType)
        {
            if (handlers == null)
                return;

            global::System.Collections.Generic.HashSet<EngineCommandType> seen =
                new global::System.Collections.Generic.HashSet<EngineCommandType>();
            for (int i = 0; i < handlers.Length; i++)
            {
                IEngineCommandHandler<EngineCommandType>? h = handlers[i];
                if (h == null)
                    continue;

                if (!seen.Add(h.CommandType))
                    throw new InvalidOperationException($"Duplicate handler registration for {h.CommandType} in {systemType.Name}");
            }
        }
    }
}
