// FILE: netlogic.core/Sim/Systems/IEngineCommandHandler.cs
// Self-describing handler objects for easy registry.
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.game;

namespace com.aqua.netlogic.command.handler
{
    public interface IEngineCommandHandler<TCommandType>
        where TCommandType : struct, Enum
    {
        TCommandType CommandType { get; }

        /// <summary>
        /// Process a single command and potentially mutate the world.
        /// ConnId is available on command.ConnId.
        /// </summary>
        void Handle(com.aqua.netlogic.sim.game.ServerModel world, EngineCommand<TCommandType> command);
    }

    public interface IEngineCommandHandler<TCommand, TCommandType> : IEngineCommandHandler<TCommandType>
        where TCommand : EngineCommand<TCommandType>
        where TCommandType : struct, Enum
    {
        void Handle(com.aqua.netlogic.sim.game.ServerModel world, TCommand command);
    }
}
