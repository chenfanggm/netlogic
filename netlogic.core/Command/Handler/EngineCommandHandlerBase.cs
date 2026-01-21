// FILE: netlogic.core/Sim/Systems/EngineCommandHandlerBase.cs
// Base class that provides safe casting + typed Enqueue.

using System;

namespace Sim.Systems
{
    public abstract class EngineCommandHandlerBase<TCommand, TCommandType>(TCommandType commandType)
        : IEngineCommandHandler<TCommand, TCommandType>
        where TCommand : EngineCommand<TCommandType>
        where TCommandType : struct, Enum
    {
        public TCommandType CommandType { get; } = commandType;

        public void Handle(Game.World world, EngineCommand<TCommandType> command)
        {
            if (command is TCommand typed)
                Handle(world, typed);
            else
                throw new InvalidOperationException($"Command {command.Type} is not of type {typeof(TCommand).Name}");
        }

        public abstract void Handle(Game.World world, TCommand command);
    }
}
