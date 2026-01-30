using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.eventbus
{
    public readonly struct GameFlowStateTransitionEvent
    {
        public GameFlowState From { get; }
        public GameFlowState To { get; }
        public int ServerTick { get; }

        public GameFlowStateTransitionEvent(GameFlowState from, GameFlowState to, int serverTick)
        {
            From = from;
            To = to;
            ServerTick = serverTick;
        }
    }
}
