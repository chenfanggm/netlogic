using MessagePipe;
using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.program
{
    internal sealed class FlowStateTransitionHandler : IMessageHandler<GameFlowStateTransition>
    {
        private readonly RenderSimulator _state;

        public FlowStateTransitionHandler(RenderSimulator state)
        {
            _state = state;
        }

        public void Handle(GameFlowStateTransition message)
        {
            _state.FlowStateChangedThisTick = true;
            _state.LeftInRoundThisTick |= message.From == GameFlowState.InRound
                && message.To != GameFlowState.InRound;
            _state.EnteredMainMenuAfterVictoryThisTick |= message.From == GameFlowState.RunVictory
                && message.To == GameFlowState.MainMenu;
            _state.LastClientFlowState = message.To;
        }
    }
}
