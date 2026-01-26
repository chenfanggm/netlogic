using Net;
using Sim.Time;

namespace Sim.Client
{
    public sealed class ClientHost : IDisposable
    {
        private readonly IClientTransport _transport;
        private readonly GameClient _client;
        private readonly TickRunner _runner;

        private int _clientTick;
        private bool _running;

        public GameClient Client => _client;

        public ClientHost(IClientTransport transport, int tickRateHz)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _client = new GameClient(_transport, tickRateHz);
            _runner = new TickRunner(tickRateHz);

            _clientTick = 0;
            _running = false;
        }

        public void Start()
        {
            _client.Start();
        }

        public void Connect(string host, int port)
        {
            _client.Connect(host, port);
            _running = true;
        }

        public void Run(CancellationToken token)
        {
            if (!_running)
                return;

            _runner.Run(
                onTick: _ =>
                {
                    // ClientTick advances at same nominal tick rate for scheduling inputs
                    _clientTick++;
                    _client.Poll(_clientTick);
                },
                token: token);
        }

        public void Dispose()
        {
            _transport.Dispose();
            _running = false;
        }
    }
}
