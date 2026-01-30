using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.systems.gameflowsystem.commands;
using com.aqua.netlogic.command.handler;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.gameflowsystem.handlers
{
    [EngineCommandHandler(typeof(GameFlowSystem))]
    internal sealed class FlowIntentHandler : EngineCommandHandlerBase<FlowIntentEngineCommand, EngineCommandType>
    {
        public FlowIntentHandler() : base(EngineCommandType.FlowFire)
        {
        }

        public override void Handle(com.aqua.netlogic.sim.game.ServerModel world, FlowIntentEngineCommand command)
        {
            if (command.Intent == GameFlowIntent.None)
                return;

            // Single entry point: the flow controller validates + applies.
            world.GameFlow.ApplyPlayerIntentFromCommand(command.Intent, command.Param0);
        }
    }
}
