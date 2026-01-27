using Net;
using Sim.Time;
using Client2.Net;

namespace Client2.Game
{
    public sealed class ClientHost2 : IDisposable
    {
        private readonly IClientTransport _transport;
        private readonly TickRunner _runner;
        private int _clientTick;
        private bool _running;

        public GameClient2 Client { get; }
        public NetworkClient2 Network { get; }

        public ClientHost2(IClientTransport transport, int tickRateHz)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Network = new NetworkClient2(_transport, tickRateHz);
            Client = new GameClient2(Network);
            _runner = new TickRunner(tickRateHz);

            _clientTick = 0;
            _running = false;
        }

        public void Start()
        {
            Client.Start();
        }

        public void Connect(string host, int port)
        {
            Client.Connect(host, port);
            _running = true;
        }

        public void Run(CancellationToken token)
        {
            if (!_running)
                return;

            _runner.Run(
                onTick: _ =>
                {
                    _clientTick++;
                    Client.Poll();
                },
                token: token);
        }

        public void Tick()
        {
            _clientTick++;
            Client.Poll();
        }

        public void Dispose()
        {
            _transport.Dispose();
            _running = false;
        }
    }
}
