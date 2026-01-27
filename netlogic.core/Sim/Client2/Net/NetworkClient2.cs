using System;
using Client2.Protocol;

namespace Client2.Net
{
    public sealed class NetworkClient2
    {
        private readonly IClientTransport _transport;

        public event Action<ServerSnapshot>? OnServerSnapshot;

        public NetworkClient2(IClientTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.OnReceive += OnReceive;
        }

        public void SendClientCommand(ClientCommand cmd)
        {
            _transport.Send(cmd);
        }

        public void Poll()
        {
            _transport.Poll();
        }

        private void OnReceive(object msg)
        {
            if (msg is ServerSnapshot snap)
                OnServerSnapshot?.Invoke(snap);
        }
    }
}
