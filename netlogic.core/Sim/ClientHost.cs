using System;
using System.Threading;
using Net;

namespace Sim
{
    public sealed class ClientHost : IDisposable
    {
        private readonly IClientTransport _transport;
        private readonly GameClient _client;
        private readonly FixedTickRunner _runner;

        private int _clientTick;
        private bool _running;

        public GameClient Client => _client;

        public ClientHost(IClientTransport transport, int tickRateHz)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _client = new GameClient(_transport, tickRateHz);
            _runner = new FixedTickRunner(tickRateHz, maxTicksPerUpdate: 4);

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
            while (!token.IsCancellationRequested && _running)
            {
                _runner.Step(
                    pollAction: PollOnly,
                    tickAction: TickOnce);

                Thread.Sleep(1);
            }
        }

        private void PollOnly()
        {
            _client.Poll(_clientTick);
        }

        private void TickOnce()
        {
            // ClientTick advances at same nominal tick rate for scheduling inputs
            _clientTick++;
            _client.Poll(_clientTick);
        }

        public void Dispose()
        {
            _transport.Dispose();
            _running = false;
        }
    }
}
