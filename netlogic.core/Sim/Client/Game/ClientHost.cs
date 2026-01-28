using Client.Net;

namespace Client.Game
{
    public sealed class ClientHost : IDisposable
    {
        private readonly IClientTransport _transport;

        public GameClient Client { get; }
        public NetworkClient Network { get; }

        public ClientHost(IClientTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Network = new NetworkClient(_transport);
            Client = new GameClient(Network);
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
