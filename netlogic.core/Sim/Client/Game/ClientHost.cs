using Client.Net;

namespace Client.Game
{
    public sealed class ClientHost : IDisposable
    {
        private readonly IClientTransport _transport;

        public NetworkClient Client { get; }

        public ClientHost(IClientTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Client = new NetworkClient(_transport);
        }

        public void Tick()
        {
            Client.Poll();
        }

        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}
