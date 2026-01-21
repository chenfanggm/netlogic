using System;
using Cysharp.Threading.Tasks;
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
        UniTask PublishAsync<TEvent>(TEvent message);

        void Publish<TTopic, TEvent>(TTopic topic, TEvent message);
        UniTask PublishAsync<TTopic, TEvent>(TTopic topic, TEvent message);

        // Subscribe
        IDisposable SubscribeAsync<TEvent>(Action<TEvent> handler);
        IDisposable SubscribeAsync<TEvent>(Func<TEvent, UniTask> handler);
        IDisposable SubscribeAsync<TTopic, TEvent>(TTopic topic, Action<TEvent> handler);
        IDisposable SubscribeAsync<TTopic, TEvent>(TTopic topic, Func<TEvent, UniTask> handler);
    }
}
