using System;
using MessagePipe;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.systems.gameflowsystem.commands;
using com.aqua.netlogic.command.events;

namespace com.aqua.netlogic.program
{
    internal sealed class FlowStateTransitionEventHandler : IMessageHandler<GameFlowStateTransitionEvent>
    {
        private readonly IEventBus _eventBus;

        public FlowStateTransitionEventHandler(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void Handle(GameFlowStateTransitionEvent message)
        {
            Console.WriteLine(
                $"[ClientModel] serverTick={message.ServerTick} FlowTo={message.To}");
            if (message.From == GameFlowState.InRound
                && message.To != GameFlowState.InRound
                && message.To != GameFlowState.MainMenu
                && message.To != GameFlowState.RunVictory)
            {
                _eventBus.Publish(new CommandEvent(
                    new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)));
            }

        }
    }
}
