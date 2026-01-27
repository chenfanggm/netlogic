using System.Diagnostics;
using Sim.Game;
using Net;
using Sim.Server;
using Client2.Game;
using Client2.Net;
using Sim.Time;

namespace Program
{
    public static class ManualTickProgram
    {
        public static void Run(int totalTicks)
        {
            // Switch transports without changing game logic:
            // true  => InProcess (fast dev)
            // false => LiteNetLib UDP (real)
            INetFactory factory = NetFactory.Choose(useInProcess: true);
            IServerTransport serverTransport = factory.CreateServerTransport();
            IClientTransport clientTransport = factory.CreateClientTransport();

            Game world = new Game();
            world.CreateEntityAt(entityId: 1, x: 0, y: 0);

            int port = 9050;
            int tickRateHz = 20;
            GameServer server = new GameServer(serverTransport, tickRateHz, world);
            server.Start(port);

            NetworkClient2 net = new NetworkClient2(clientTransport, tickRateHz);
            GameClient2 client = new GameClient2(net);
            client.Start();
            client.Connect(host: "127.0.0.1", port);

            Stopwatch time = Stopwatch.StartNew();

            double lastTickAtMs = time.Elapsed.TotalMilliseconds;

            uint clientCmdSeq = 1;

            for (int i = 0; i < totalTicks; i++)
            {
                // Poll network
                server.Poll();
                client.Poll();

                // Send input every 5 ticks
                if ((i % 5) == 0)
                {
                    int targetServerTick = client.Model.LastServerTick + 1;
                    client.SendMoveBy(
                        targetServerTick: targetServerTick,
                        clientCmdSeq: clientCmdSeq++,
                        entityId: 1,
                        dx: 1,
                        dy: 0);
                }

                // Advance server tick
                double nowMs = time.Elapsed.TotalMilliseconds;
                double elapsedSinceLastTickMs = nowMs - lastTickAtMs;
                long serverTimeMs = (long)nowMs;
                lastTickAtMs = nowMs;

                TickContext ctx = new TickContext(
                    serverTimeMs: serverTimeMs,
                    elapsedMsSinceLastTick: elapsedSinceLastTickMs);
                server.TickOnce(ctx);

                // Poll again to receive updates
                server.Poll();
                client.Poll();

                if ((i % 10) == 0)
                {
                    if (client.Model.Entities.TryGetValue(1, out EntityState e0))
                    {
                        Console.WriteLine(
                            "Tick=" + i
                            + " Entity1=(" + e0.X + "," + e0.Y + ")");
                    }
                }
            }

            serverTransport.Dispose();
            clientTransport.Dispose();
        }
    }
}
