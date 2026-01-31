using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    public readonly struct EntitySpawnedEvent
    {
        public readonly int EntityId;
        public readonly int ServerTick;

        public EntitySpawnedEvent(int entityId, int serverTick)
        {
            EntityId = entityId;
            ServerTick = serverTick;
        }
    }

    public sealed class EntitySpawnedHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
            ctx.EventBus.Publish(new EntitySpawnedEvent(op.EntityId, ctx.ServerTick));
        }
    }
}
