using Sim.Game;
using Sim.Game.Flow;
using Sim.System;

namespace Sim.Command
{
    [EngineCommandHandler(typeof(GameFlowSystem))]
    internal sealed class FlowIntentHandler : EngineCommandHandlerBase<FlowIntentEngineCommand, EngineCommandType>
    {
        public FlowIntentHandler() : base(EngineCommandType.FlowFire)
        {
        }

        public override void Handle(Game.Game world, FlowIntentEngineCommand command)
        {
            if (command.Intent == GameFlowIntent.None)
                return;

            // Single entry point: the flow controller validates + applies.
            world.Flow.ApplyPlayerIntentFromCommand(command.Intent, command.Param0);
        }
    }
}
