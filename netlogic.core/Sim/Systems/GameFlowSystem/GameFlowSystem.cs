using Sim.Game;

namespace Sim.Command
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
