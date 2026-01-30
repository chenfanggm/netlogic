using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.eventbus
{
    public readonly struct GameFlowStateTransition
    {
        public GameFlowState From { get; }
        public GameFlowState To { get; }
        public int ServerTick { get; }

        public GameFlowStateTransition(GameFlowState from, GameFlowState to, int serverTick)
        {
            From = from;
            To = to;
            ServerTick = serverTick;
        }
    }
}
