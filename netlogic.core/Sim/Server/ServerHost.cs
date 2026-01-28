using Net;
using Sim.Game;
using Sim.Time;

namespace Sim.Server
{
    /// <summary>
    /// Real-time server host:
    /// - Owns transport + NetworkServer
    /// - Runs Poll + TickOnce on a fixed clock (real time)
    /// Keeps NetworkServer pure and testable.
    /// </summary>
    public sealed class ServerHost : IDisposable
    {
        private readonly IServerTransport _transport;
        private readonly NetworkServer _server;
        private readonly TickRunner _runner;

        private bool _running;

        public ServerHost(IServerTransport transport, int tickRateHz, Game.Game game)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _server = new NetworkServer(_transport, tickRateHz, game);
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
