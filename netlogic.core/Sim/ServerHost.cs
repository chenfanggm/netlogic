using System;
using System.Threading;
using Game;
using Net;

namespace Sim
{
    /// <summary>
    /// Real-time server host:
    /// - Owns transport + GameServer
    /// - Runs Poll + TickOnce on a fixed clock (real time)
    /// Keeps GameServer pure and testable.
    /// </summary>
    public sealed class ServerHost : IDisposable
    {
        private readonly IServerTransport _transport;
        private readonly GameServer _server;
        private readonly FixedTickRunner _runner;

        private bool _running;

        public ServerHost(IServerTransport transport, int tickRateHz, World world)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _server = new GameServer(_transport, tickRateHz, world);
            _runner = new FixedTickRunner(tickRateHz, maxTicksPerUpdate: 4);

            _running = false;
        }

        public void Start(int port)
        {
            _server.Start(port);
            _running = true;
        }

        public void Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _running)
            {
                _runner.Step(
                    pollAction: _server.Poll,
                    tickAction: _server.TickOnce);

                // Light sleep so we don't pin a CPU core in console mode
                Thread.Sleep(1);
            }
        }

        public void Dispose()
        {
            _transport.Dispose();
            _running = false;
        }
    }
}
