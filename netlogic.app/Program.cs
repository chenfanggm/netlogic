using System;
using Net;
using Sim;

namespace App
{
    public static class Program
    {
        public static void Main()
        {
            InProcessTransportLink link = new InProcessTransportLink();

            TickClock serverClock = new TickClock(tickRateHz: 20);
            ServerSim server = new ServerSim(serverClock, link.ServerEnd);

            ClientSim client = new ClientSim(link.ClientEnd)
            {
                InputDelayTicks = 3,
                RenderDelayTicks = 3
            };

            client.Connect("Alice");

            int totalTicks = 160; // ~8 seconds at 20Hz

            int i = 0;
            while (i < totalTicks)
            {
                client.PumpNetwork();

                // Send a move command every 10 server ticks (approx)
                if (i % 10 == 0)
                {
                    client.SendMoveCommand(entityId: 1, dx: 1, dy: 0);
                }

                // Run one authoritative server tick
                server.RunTicks(1);

                // Pump incoming snapshot
                client.PumpNetwork();

                // Render (interpolated)
                EntityState[] renderEntities = client.GetRenderEntities();
                if (renderEntities.Length > 0 && i % 5 == 0)
                {
                    EntityState e1 = renderEntities[0];
                    int estTick = client.GetEstimatedServerTickFloor();
                    Console.WriteLine("EstServerTick=" + estTick + " RenderEntity1=(" + e1.X + "," + e1.Y + ") Hp=" + e1.Hp);
                }

                i++;
            }
        }
    }
}
