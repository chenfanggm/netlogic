using System;
using Net;
using Client.Net;

namespace Client.Game
{
    public sealed class ClientHost : IDisposable
    {
        private readonly global::Net.IClientTransport _transport;

        public NetworkClient Client { get; }

        public ClientHost(global::Net.IClientTransport transport, int clientTickRateHz = 60)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Client = new NetworkClient(_transport, clientTickRateHz);
        }

        public void StartAndConnect(string host, int port)
        {
            Client.Start();
            Client.Connect(host, port);
        }

        public void Tick()
        {
            Client.Poll();
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
