using Sim.Game;

namespace Sim.Command
{
    public sealed class MovementSystem : CommandSinkBase<EngineCommandType>
    {
        public MovementSystem() : base(handlers: EngineCommandHandlerRegistry.ForMovementSystem())
        {
            EngineCommandHandlerRegistry.ValidateNoDuplicateCommandTypes(
                EngineCommandHandlerRegistry.ForMovementSystem(),
                typeof(MovementSystem));
        }
    }
}
