using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    public sealed class PositionSnapshotHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }
}
