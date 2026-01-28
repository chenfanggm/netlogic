using System;

namespace Client.Net
{
    /// <summary>
    /// Deterministic "no real network" transport.
    /// Wraps InProcessNetFeed so client code only depends on IClientTransport.
    /// </summary>
    [global::System.Obsolete("Legacy object/message transport. Use Net.IClientTransport + InProcessPacketTransportPair.")]
    public sealed class InProcessClientTransport : IClientTransport
    {
        public event Action<object>? OnReceive;

        private readonly InProcessNetFeed _feed;

        public InProcessClientTransport(InProcessNetFeed feed)
        {
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _feed.OnClientReceive += HandleInbound;
        }

        public void Send<T>(T msg) where T : class
        {
            _feed.SendFromClient(msg);
        }

        public void Poll()
        {
            _feed.Poll();
        }

        public void Dispose()
        {
            _feed.OnClientReceive -= HandleInbound;
        }

        private void HandleInbound(object msg)
        {
            OnReceive?.Invoke(msg);
        }
    }
}
