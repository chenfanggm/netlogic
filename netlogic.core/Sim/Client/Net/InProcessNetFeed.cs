using System;

namespace Client.Net
{
    /// <summary>
    /// Deterministic in-process message bus for client/server wiring.
    /// </summary>
    [global::System.Obsolete("Legacy object/message transport. Use Net.IClientTransport + InProcessPacketTransportPair.")]
    public sealed class InProcessNetFeed
    {
        public event Action<object>? OnClientReceive;
        public event Action<object>? OnServerReceive;

        public void SendFromServer(object msg)
        {
            OnClientReceive?.Invoke(msg);
        }

        public void SendFromClient(object msg)
        {
            OnServerReceive?.Invoke(msg);
        }

        public void Poll()
        {
            // No-op: event-driven by default.
        }
    }
}
