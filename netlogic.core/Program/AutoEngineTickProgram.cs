using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Game;
using Sim;

namespace Program
{
    /// <summary>
    /// No-netcode harness: drives ServerEngine at a fixed tick rate,
    /// injects MoveBy commands, and prints snapshot state periodically.
    /// </summary>
    public static class AutoEngineTickProgram
    {
        private sealed class LatestTick
        {
            public EngineTickResult Value;
        }

        public static void Run(TimeSpan? maxRunningDuration = null)
        {
            const int tickRateHz = 20;
            const int entityId = 1;
            const int connId = 1;

            // Setup world + engine (no transport)
            World world = new World();
            world.CreateEntityAt(entityId: entityId, x: 0, y: 0);

            ServerEngine engine = new ServerEngine(tickRateHz, world);

            using CancellationTokenSource cts = new CancellationTokenSource();
            if (maxRunningDuration.HasValue)
                cts.CancelAfter(maxRunningDuration.Value);

            // Published by engine thread
            LatestTick latest = new LatestTick();
            int latestTick = -1;

            Thread engineThread = new Thread(() =>
            {
                TickLoop(engine, cts.Token, r =>
                {
                    Volatile.Write(ref latest, new LatestTick { Value = r });
                    Volatile.Write(ref latestTick, r.ServerTick);
                });
            })
            {
                IsBackground = true,
                Name = "AutoEngineTick.Engine"
            };

            Thread inputThread = new Thread(() => InputLoop(engine, cts.Token, connId, entityId))
            {
                IsBackground = true,
                Name = "AutoEngineTick.Input"
            };

            engineThread.Start();
            inputThread.Start();

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    // Wait until we have at least one real tick result
                    if (Volatile.Read(ref latestTick) >= 0)
                    {
                        LatestTick snapshot = Volatile.Read(ref latest);
                        Print(snapshot.Value, entityId);
                    }
                    else
                    {
                        Console.WriteLine("Waiting for first tick...");
                    }
                    Thread.Sleep(500);
                }
            }
            finally
            {
                // Ensure threads stop and join
                cts.Cancel();
                engineThread.Join();
                inputThread.Join();
            }
        }

        private static void InputLoop(ServerEngine engine, CancellationToken token, int connId, int entityId)
        {
            uint seq = 0;

            while (!token.IsCancellationRequested)
            {
                int requestedTick = engine.CurrentServerTick + 1;

                List<EngineCommand<EngineCommandType>> cmds =
                [
                    new MoveByEngineCommand(entityId: entityId, dx: 1, dy: 0)
                ];

                engine.EnqueueCommands(
                    connId: connId,
                    requestedClientTick: requestedTick,
                    clientCmdSeq: seq++,
                    commands: cmds);

                Thread.Sleep(250);
            }
        }

        private static void TickLoop(ServerEngine engine, CancellationToken token, Action<EngineTickResult> publish)
        {
            double tickMs = 1000.0 / engine.TickRateHz;
            long freq = Stopwatch.Frequency;
            long next = Stopwatch.GetTimestamp();

            while (!token.IsCancellationRequested)
            {
                publish(engine.TickOnce());

                // Drift-corrected fixed-rate loop
                next += (long)(freq * (tickMs / 1000.0));
                while (!token.IsCancellationRequested)
                {
                    long now = Stopwatch.GetTimestamp();
                    long remaining = next - now;
                    if (remaining <= 0)
                        break;

                    int remainingMs = (int)(remaining * 1000 / freq);
                    if (remainingMs > 1)
                        Thread.Sleep(remainingMs - 1);
                    else
                        Thread.Yield();
                }
            }
        }

        private static void Print(EngineTickResult r, int entityId)
        {
            SampleWorldSnapshot? snap = r.Snapshot;
            if (snap == null)
            {
                Console.WriteLine($"Tick={r.ServerTick} TimeMs={r.ServerTimeMs} Snapshot=<null>");
                return;
            }

            if (TryGetEntityPos(snap, entityId, out int x, out int y))
            {
                FlowSnapshot flow = snap.Flow;
                Console.WriteLine(
                    $"Tick={r.ServerTick} TimeMs={r.ServerTimeMs} " +
                    $"Entity{entityId}=({x},{y}) " +
                    $"Flow={flow.FlowState} L{flow.LevelIndex} R{flow.RoundIndex} " +
                    $"RoundState={flow.RoundState} Score={flow.CumulativeScore}/{flow.TargetScore} " +
                    $"CookSeq={flow.CookResultSeq}");
            }
            else
            {
                Console.WriteLine(
                    $"Tick={r.ServerTick} TimeMs={r.ServerTimeMs} Entity{entityId}=<not found> Flow={snap.Flow.FlowState}");
            }
        }

        private static bool TryGetEntityPos(SampleWorldSnapshot snap, int entityId, out int x, out int y)
        {
            SampleEntityPos[]? arr = snap.Entities;
            if (arr == null || arr.Length == 0)
            {
                x = 0;
                y = 0;
                return false;
            }
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].EntityId == entityId)
                {
                    x = arr[i].X;
                    y = arr[i].Y;
                    return true;
                }
            }

            x = 0;
            y = 0;
            return false;
        }
    }
}
