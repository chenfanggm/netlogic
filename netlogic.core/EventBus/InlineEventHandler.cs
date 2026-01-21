using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace com.aqua.system
{
    public sealed class InlineAsyncHandler<TEvent> : IAsyncMessageHandler<TEvent>
    {
        private readonly Func<TEvent, CancellationToken, UniTask> _handler;

        public InlineAsyncHandler(Func<TEvent, CancellationToken, UniTask> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public InlineAsyncHandler(Func<TEvent, UniTask> handler)
        {
            _handler = (message, token) => handler(message);
        }

        public InlineAsyncHandler(Action<TEvent> handler)
        {
            _handler = (message, token) =>
            {
                handler(message);
                return UniTask.CompletedTask;
            };
        }

        public UniTask HandleAsync(TEvent message, CancellationToken cancellationToken)
        {
            return _handler(message, cancellationToken);
        }
    }

    public sealed class InlineSyncHandler<TEvent> : IMessageHandler<TEvent>
    {
        private readonly Action<TEvent> _handler;

        public InlineSyncHandler(Action<TEvent> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Handle(TEvent message)
        {
            _handler(message);
        }
    }
}
