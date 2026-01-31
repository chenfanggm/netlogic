using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.systems.gameflowsystem.commands;
using com.aqua.netlogic.command.handler;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.gameflowsystem.handlers
{
    [EngineCommandHandler(typeof(GameFlowSystem))]
    internal sealed class FlowIntentHandler : EngineCommandHandlerBase<FlowIntentEngineCommand, EngineCommandType>
    {
        public FlowIntentHandler() : base(EngineCommandType.FlowFire)
        {
        }

        public override void Handle(com.aqua.netlogic.sim.game.ServerModel world, OpWriter ops, FlowIntentEngineCommand command)
        {
            if (command.Intent == GameFlowIntent.None)
                return;

            ops.EmitAndApply(RepOp.FlowFire((byte)command.Intent, command.Param0));
        }
    }
}
