using System;
using Game;
using Net;
using Sim;

namespace App
{
    public static class Program
    {
        public static void Main()
        {
            // Toggle this to switch transports without changing game logic:
            // true  => InProcess transport (fast dev iteration)
            // false => LiteNetLib UDP transport (real-world behavior)
            bool useInProcess = true;

            INetFactory factory;
            IServerTransport serverTransport;
            IClientTransport clientTransport;

            if (useInProcess)
            {
                InProcessTransportPair pair = new InProcessTransportPair();
                factory = new InProcessNetFactory(pair);
            }
            else
            {
                factory = new LiteNetLibNetFactory();
            }

            serverTransport = factory.CreateServerTransport();
            clientTransport = factory.CreateClientTransport();

            TickClock serverClock = new TickClock(tickRateHz: 20);

            World world = new World();
            world.CreateEntityAt(entityId: 1, x: 0, y: 0);

            GameServer server = new GameServer(serverTransport, serverClock, world);

            int port = 9050;
            server.Start(port);

            GameClient client = new GameClient(clientTransport, tickRateHz: 20);
            client.Start();
            client.Connect("127.0.0.1", port);

            int i = 0;
            int totalTicks = 200;

            while (i < totalTicks)
            {
                // Poll both ends
                server.Poll();
                client.Poll();

                // Send a move input every 10 ticks
                if (i % 10 == 0)
                {
                    client.SendMoveBy(clientTick: i, entityId: 1, dx: 1, dy: 0);
                }

                // Advance server sim by 1 tick
                server.TickOnce();

                // Poll again to receive updates
                server.Poll();
                client.Poll();

                if (i % 10 == 0)
                {
                    EntityState[] render = client.GetRenderEntities();
                    if (render.Length > 0)
                    {
                        EntityState e0 = render[0];
                        Console.WriteLine("Tick=" + i + " RenderEntity1=(" + e0.X + "," + e0.Y + ")");
                    }
                }

                i++;
            }

            serverTransport.Dispose();
            clientTransport.Dispose();
        }
    }
}
