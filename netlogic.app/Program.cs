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
            // 20% packet loss in both directions to stress reliability
            LossyInProcessTransportLink link = new LossyInProcessTransportLink(
                lossClientToServer: 0.20,
                lossServerToClient: 0.20,
                randomSeed: 12345);

            TickClock serverClock = new TickClock(tickRateHz: 20);
            ServerSim server = new ServerSim(serverClock, link.ServerEnd);

            ClientSim client = new ClientSim(link.ClientEnd)
            {
                InputDelayTicks = 3,
                RenderDelayTicks = 3,

                ResendIntervalMs = 120,
                MaxResendsPerPump = 8
            };

            client.Connect("Alice");

            int totalTicks = 220; // ~11 seconds at 20Hz

            for (int i = 0; i < totalTicks; i++)
            {
                // Client: pump + resend
                client.PumpNetworkAndResends();

                // Send a move command periodically
                if (i % 10 == 0)
                    client.SendMoveCommand(entityId: 1, dx: 1, dy: 0);

                // Server: one authoritative tick
                server.RunTicks(1);

                // Client: receive snapshots + resend again
                client.PumpNetworkAndResends();

                // Render (interpolated)
                if (i % 10 == 0)
                {
                    Net.EntityState[] renderEntities = client.GetRenderEntities();
                    if (renderEntities.Length > 0)
                    {
                        Net.EntityState e1 = renderEntities[0];
                        int estTick = client.GetEstimatedServerTickFloor();
                        Console.WriteLine("EstServerTick=" + estTick + " RenderEntity1=(" + e1.X + "," + e1.Y + ")");
                    }
                }
            }
        }
    }
}
