using Client.Net;

namespace Client.Game
{
    public sealed class ClientHost2 : IDisposable
    {
        private readonly IClientTransport _transport;

        public GameClient2 Client { get; }
        public NetworkClient2 Network { get; }

        public ClientHost2(IClientTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Network = new NetworkClient2(_transport);
            Client = new GameClient2(Network);
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
