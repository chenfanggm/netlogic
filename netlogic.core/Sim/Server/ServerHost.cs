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
        private readonly TickRunner _runner;

        private bool _running;

        public ServerHost(IServerTransport transport, int tickRateHz, World world)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _server = new GameServer(_transport, tickRateHz, world);
            _runner = new TickRunner(tickRateHz);

            _running = false;
        }

        public void Start(int port)
        {
            _server.Start(port);
            _running = true;
        }

        public void Run(CancellationToken token)
        {
            if (!_running)
                return;

            _runner.Run(
                onTick: ctx =>
                {
                    _server.Poll();
                    _server.TickOnce(ctx);
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
