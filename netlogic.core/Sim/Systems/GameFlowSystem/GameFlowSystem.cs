using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.command.sink;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.gameflowsystem
{
    public sealed class GameFlowSystem : CommandSinkBase<EngineCommandType>
    {
        public GameFlowSystem() : base(handlers: EngineCommandHandlerRegistry.ForGameFlowSystem())
        {
            EngineCommandHandlerRegistry.ValidateNoDuplicateCommandTypes(
                EngineCommandHandlerRegistry.ForGameFlowSystem(),
                typeof(GameFlowSystem));
        }
    }
}
