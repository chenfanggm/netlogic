using Game;
using Game.Flow;

namespace Sim.Systems
{
    [EngineCommandHandler(typeof(GameFlowSystem))]
    internal sealed class FlowIntentHandler : EngineCommandHandlerBase<FlowIntentEngineCommand>
    {
        public FlowIntentHandler() : base(EngineCommandType.FlowFire)
        {
        }

        public override void Handle(World world, FlowIntentEngineCommand command)
        {
            if (command.Intent == GameFlowIntent.None)
                return;

            // Single entry point: the flow controller validates + applies.
            world.Flow.ApplyPlayerIntentFromCommand(command.Intent, command.Param0);
        }
    }
}
