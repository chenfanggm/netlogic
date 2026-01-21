using MessagePipe;

namespace com.aqua.system
{
    /// <summary>
    /// Manages all puzzle-related events
    /// Separates event concerns from business logic
    /// </summary>
    public interface IEventBus
    {
        // Publish
        void Publish<TEvent>(TEvent message);

        void Publish<TTopic, TEvent>(TTopic topic, TEvent message) where TTopic : notnull;

        // Subscribe
        IDisposable Subscribe<TEvent>(IMessageHandler<TEvent> handler);
        IDisposable Subscribe<TTopic, TEvent>(TTopic topic, IMessageHandler<TEvent> handler) where TTopic : notnull;
    }
}
