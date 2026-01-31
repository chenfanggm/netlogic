using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    public readonly struct EntityDestroyedEvent
    {
        public readonly int EntityId;
        public readonly int ServerTick;

        public EntityDestroyedEvent(int entityId, int serverTick)
        {
            EntityId = entityId;
            ServerTick = serverTick;
        }
    }

    public sealed class EntityDestroyedHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
            ctx.EventBus.Publish(new EntityDestroyedEvent(op.EntityId, ctx.ServerTick));
        }
    }
}
