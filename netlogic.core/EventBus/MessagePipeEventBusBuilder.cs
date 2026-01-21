using System;
using MessagePipe;

namespace com.aqua.system
{
    /// <summary>
    /// Builder for configuring MessagePipe-based puzzle event bus
    /// Hides GlobalMessagePipe implementation details and provides fluent API
    /// </summary>
    public class MessagePipeEventBusBuilder
    {
        private readonly BuiltinContainerBuilder _builder;
        private bool _isBuilt = false;

        public MessagePipeEventBusBuilder()
        {
            _builder = new BuiltinContainerBuilder();
            _builder.AddMessagePipe();
        }

        /// <summary>
        /// Register a message broker for a specific event type
        /// </summary>
        public MessagePipeEventBusBuilder RegisterEvent<TEvent>()
        {
            if (_isBuilt)
                throw new InvalidOperationException(
                    "Cannot register events after the builder has been built."
                );

            _builder.AddMessageBroker<TEvent>();
            return this;
        }

        public MessagePipeEventBusBuilder RegisterEvent<TTopic, TEvent>()
        {
            if (_isBuilt)
                throw new InvalidOperationException(
                    "Cannot register events after the builder has been built."
                );

            _builder.AddMessageBroker<TTopic, TEvent>();
            return this;
        }

        /// <summary>
        /// Build and return the configured puzzle event bus
        /// </summary>
        public IEventBus Build()
        {
            if (_isBuilt)
                throw new InvalidOperationException("Builder has already been built.");

            _isBuilt = true;
            var provider = _builder.BuildServiceProvider();
            GlobalMessagePipe.SetProvider(provider);

            return new MessagePipeEventBus();
        }
    }
}
