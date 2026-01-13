using System;
using Net;
using Sim;

namespace App
{
    /// <summary>
    /// Main application entry point that demonstrates the networked simulation system.
    /// </summary>
    public static class Program
    {
        public static void Main()
        {
            LossyInProcessTransportLink link = new LossyInProcessTransportLink(
                lossClientToServer: 0.10,
                lossServerToClient: 0.10,
                randomSeed: 222);

            TickClock serverClock = new TickClock(tickRateHz: 20);
            ServerSim server = new ServerSim(serverClock, link.ServerEnd)
            {
                FullSnapshotIntervalTicks = 20 // periodic baseline
            };

            ClientSim client = new ClientSim(link.ClientEnd)
            {
                InputDelayTicks = 3,
                RenderDelayTicks = 3,
                ResendIntervalMs = 120
            };

            client.Connect("Alice");

            int totalTicks = 260;
            int i = 0;

            while (i < totalTicks)
            {
                client.PumpNetworkAndResends();

                if (i % 10 == 0)
                    client.SendMoveCommand(entityId: 1, dx: 1, dy: 0);

                server.RunTicks(1);

                client.PumpNetworkAndResends();

                if (i % 10 == 0)
                {
                    EntityState[] renderEntities = client.GetRenderEntities();
                    if (renderEntities.Length > 0)
                    {
                        EntityState e1 = renderEntities[0];
                        int estTick = client.GetEstimatedServerTickFloor();
                        Console.WriteLine("EstServerTick=" + estTick + " RenderEntity1=(" + e1.X + "," + e1.Y + ")");
                    }
                }

                i++;
            }
        }
    }
}
