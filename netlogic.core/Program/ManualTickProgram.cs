using System.Diagnostics;
using Game;
using Net;
using Sim;

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

            World world = new World();
            world.CreateEntityAt(entityId: 1, x: 0, y: 0);

            int port = 9050;
            int tickRateHz = 20;
            GameServer server = new GameServer(serverTransport, tickRateHz, world);
            server.Start(port);

            GameClient client = new GameClient(clientTransport, tickRateHz);
            client.Start();
            client.Connect(host: "127.0.0.1", port);

            Stopwatch time = Stopwatch.StartNew();

            for (int i = 0; i < totalTicks; i++)
            {
                // Poll network
                server.Poll();
                client.Poll(clientTick: i);

                // Send input every 5 ticks
                if ((i % 5) == 0)
                {
                    client.SendMoveBy(clientTick: i, entityId: 1, dx: 1, dy: 0);
                }

                // Advance server tick
                TickContext ctx = new TickContext(
                    tick: i + 1,
                    tickRateHz: tickRateHz,
                    tickDurationMs: 1000.0 / tickRateHz,
                    serverTimeMs: time.ElapsedMilliseconds);
                server.TickOnce(ctx);

                // Poll again to receive updates
                server.Poll();
                client.Poll(clientTick: i);

                if ((i % 10) == 0)
                {
                    EntityState[] render = client.GetRenderEntities();
                    if (render.Length > 0)
                    {
                        EntityState e0 = render[0];
                        Console.WriteLine(
                            "Tick=" + i
                            + " RenderDelay=" + client.RenderDelayTicks
                            + " InputDelay=" + client.InputDelayTicks
                            + " Entity1=(" + e0.X + "," + e0.Y + ")");
                    }
                }
            }

            serverTransport.Dispose();
            clientTransport.Dispose();
        }
    }
}
