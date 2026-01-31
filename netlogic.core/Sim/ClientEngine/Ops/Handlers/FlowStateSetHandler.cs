using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    public sealed class FlowStateSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            GameFlowState prev = ctx.Model.FlowState;
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);

            GameFlowState next = ctx.Model.FlowState;
            if (next != prev)
            {
                ctx.EventBus.Publish(new GameFlowStateTransitionEvent(
                    prev,
                    next,
                    ctx.ServerTick));
            }
        }
    }
}
