using System;
using MessagePipe;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.program
{
    internal sealed class FlowStateTransitionEventHandler : IMessageHandler<GameFlowStateTransitionEvent>
    {
        private readonly RenderSimulator _state;

        public FlowStateTransitionEventHandler(RenderSimulator state)
        {
            _state = state;
        }

        public void Handle(GameFlowStateTransitionEvent message)
        {
            _state.FlowStateChangedThisTick = true;
            _state.LeftInRoundThisTick |= message.From == GameFlowState.InRound
                && message.To != GameFlowState.InRound;
            _state.EnteredMainMenuAfterVictoryThisTick |= message.From == GameFlowState.RunVictory
                && message.To == GameFlowState.MainMenu;
            _state.LastClientFlowState = message.To;

            Console.WriteLine(
                $"[ClientModel] serverTick={message.ServerTick} FlowTo={message.To}");
        }
    }
}
