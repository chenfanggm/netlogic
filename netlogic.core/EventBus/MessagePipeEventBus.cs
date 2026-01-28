using MessagePipe;

namespace com.aqua.netlogic.eventbus
{
    /// <summary>
    /// MessagePipe-based event manager
    /// </summary>
    public class MessagePipeEventBus : IEventBus
    {
        // ========== Register Event ==========
        
        
        // ========== Publish ==========
        public void Publish<TEvent>(TEvent message)
        {
            GlobalMessagePipe.GetPublisher<TEvent>().Publish(message);
        }

        public void Publish<TTopic, TEvent>(TTopic topic, TEvent message) where TTopic : notnull
        {
            GlobalMessagePipe.GetPublisher<TTopic, TEvent>().Publish(topic, message);
        }

        // ========== Subscribe (async) ==========
        public IDisposable Subscribe<TEvent>(IMessageHandler<TEvent> handler)
        {
            return GlobalMessagePipe.GetSubscriber<TEvent>().Subscribe(handler);
        }

        public IDisposable Subscribe<TTopic, TEvent>(TTopic topic, IMessageHandler<TEvent> handler) where TTopic : notnull
        {
            return GlobalMessagePipe.GetSubscriber<TTopic, TEvent>().Subscribe(topic, handler);
        }
    }
}
