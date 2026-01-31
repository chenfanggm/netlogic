using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    /// <summary>
    /// Client-side RepOp handler.
    /// One handler per RepOpType. The handler may:
    /// - apply authoritative mutation to ClientModel
    /// - emit presentation events (via EventBus)
    /// </summary>
    public interface IClientRepOpHandler
    {
        void Apply(in ClientOpContext ctx, in RepOp op);
    }

    public readonly struct ClientOpContext
    {
        public readonly IEventBus EventBus;
        public readonly ClientModel Model;
        public readonly int ServerTick;

        public ClientOpContext(IEventBus eventBus, ClientModel model, int serverTick)
        {
            EventBus = eventBus;
            Model = model;
            ServerTick = serverTick;
        }
    }
}
