using System.Diagnostics;
using Sim.Game;
using Net;
using Sim.Server;
using Client2.Game;

namespace Program
{
    public static class AutoTickProgram
    {
        private static CancellationTokenSource? _currentCts;
        private static readonly object _lock = new object();

        public static void Run(TimeSpan? maxRunningDuration = null)
        {
            // Switch transports without changing game logic:
            // true  => InProcess (fast dev)
            // false => LiteNetLib UDP (real)
            INetFactory factory = NetFactory.Choose(useInProcess: true);
            IServerTransport serverTransport = factory.CreateServerTransport();
            IClientTransport clientTransport = factory.CreateClientTransport();

            Game game = new Game();
            game.CreateEntityAt(entityId: 1, x: 0, y: 0);

            int port = 9050;
            int tickRateHz = 20;

            CancellationTokenSource cts;
            lock (_lock)
            {
                _currentCts?.Dispose();
                cts = new CancellationTokenSource();
                _currentCts = cts;
            }

            try
            {
                using ServerHost serverHost = new ServerHost(serverTransport, tickRateHz, game);
                serverHost.Start(port);

                using ClientHost2 clientHost = new ClientHost2(clientTransport, tickRateHz);
                clientHost.Start();
                clientHost.Connect(host: "127.0.0.1", port);

                // Demo input thread: send move every 250ms
                uint clientCmdSeq = 1;
                Thread inputThread = new Thread(() =>
                {
                    int t = 0;
                    while (!cts.IsCancellationRequested)
                    {
                        // You can swap this with your real input sampling
                        int targetServerTick = clientHost.Client.Model.LastServerTick + 1;
                        clientHost.Client.SendMoveBy(
                            targetServerTick: targetServerTick,
                            clientCmdSeq: clientCmdSeq++,
                            entityId: 1,
                            dx: 1,
                            dy: 0);
                        t += 5;
                        Thread.Sleep(250);
                    }
                });

                inputThread.Start();

                // Run both loops on background threads
                Thread serverThread = new Thread(() => serverHost.Run(cts.Token));
                Thread clientThread = new Thread(() => clientHost.Run(cts.Token));

                serverThread.Start();
                clientThread.Start();

                // Duration monitoring thread (if maxRunningDuration is specified)
                Thread? durationThread = null;
                if (maxRunningDuration.HasValue)
                {
                    durationThread = new Thread(() =>
                    {
                        Thread.Sleep(maxRunningDuration.Value);
                        if (!cts.IsCancellationRequested)
                        {
                            Stop();
                        }
                    });
                    durationThread.Start();
                }

                // Print render state periodically
                Stopwatch sw = Stopwatch.StartNew();
                while (!cts.IsCancellationRequested)
                {
                    if (clientHost.Client.Model.Entities.TryGetValue(1, out EntityState e0))
                    {
                        Console.WriteLine("Entity1=(" + e0.X + "," + e0.Y + ")");
                    }

                    // Check if we've exceeded max duration
                    if (maxRunningDuration.HasValue && sw.Elapsed >= maxRunningDuration.Value)
                    {
                        break;
                    }

                    Thread.Sleep(500);
                }

                Stop();

                serverThread.Join();
                clientThread.Join();
                inputThread.Join();
                durationThread?.Join();
            }
            finally
            {
                lock (_lock)
                {
                    if (_currentCts == cts)
                    {
                        _currentCts = null;
                    }
                    cts.Dispose();
                }
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                _currentCts?.Cancel();
            }
        }
    }
}
