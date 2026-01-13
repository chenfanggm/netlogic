using System;
using Net;
using Sim;

namespace App
{
    public static class Program
    {
        public static void Main()
        {
            InProcessTransportLink link = new();

            TickClock serverClock = new(tickRateHz: 20);
            ServerSim server = new(serverClock, link.ServerEnd);

            ClientSim client = new(link.ClientEnd);
            client.Connect("Alice");

            // Run a small simulation: each server tick, client pumps + occasionally sends a command
            for (int i = 0; i < 120; i++) // ~6 seconds at 20Hz
            {
                // client reads server messages
                client.PumpNetwork();

                // every 10 ticks, move entity 1
                if (i % 10 == 0)
                    client.SendMoveCommand(entityId: 1, dx: 1, dy: 0);

                // server runs exactly 1 tick
                server.RunTicks(1);

                // client receives snapshot after server tick
                client.PumpNetwork();

                if (client.LastSnapshot is { } snap && i % 10 == 0)
                {
                    EntityState e0 = snap.Entities.Length > 0 ? snap.Entities[0] : default;
                    Console.WriteLine($"Tick={snap.Tick} Entity1=({e0.X},{e0.Y}) Hp={e0.Hp}");
                }
            }
        }
    }
}
