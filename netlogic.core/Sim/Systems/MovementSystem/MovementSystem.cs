using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.command.sink;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.movementsystem
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
