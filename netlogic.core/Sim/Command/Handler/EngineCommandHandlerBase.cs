// FILE: netlogic.core/Sim/Systems/EngineCommandHandlerBase.cs
// Base class that provides safe casting + typed Enqueue.

namespace Sim.Systems
{
    public abstract class EngineCommandHandlerBase<TCommand>(EngineCommandType commandType) : IEngineCommandHandler<TCommand>
        where TCommand : EngineCommand
    {
        public EngineCommandType CommandType { get; } = commandType;

        public void Handle(Game.World world, EngineCommand command)
        {
            if (command is TCommand typed)
            {
                Handle(world, typed);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Command {command.Type} is not of type {typeof(TCommand).Name}");
            }
        }

        public abstract void Handle(Game.World world, TCommand command);
    }
}
