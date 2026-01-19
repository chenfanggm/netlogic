// FILE: netlogic.core/Sim/Systems/IEngineCommandHandler.cs
// Self-describing handler objects for easy registry.

using Game;

namespace Sim.Systems
{
    public interface IEngineCommandHandler
    {
        EngineCommandType CommandType { get; }

        /// <summary>
        /// Process a single command and potentially mutate the world.
        /// ConnId is available on command.ConnId.
        /// </summary>
        void Handle(World world, EngineCommand command);
    }

    public interface IEngineCommandHandler<TCommand> : IEngineCommandHandler
        where TCommand : EngineCommand
    {
        void Handle(World world, TCommand command);
    }
}
