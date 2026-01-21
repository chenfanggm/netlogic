using System.Diagnostics;
using Game;
using Sim;

namespace Program
{
    /// <summary>
    /// No-netcode harness: directly drives ServerEngine at a fixed tick rate,
    /// injects commands via ServerEngine.EnqueueCommands, and prints snapshot state.
    /// </summary>
    public static class AutoEngineTickProgram
    {
        private static CancellationTokenSource? _currentCts;
        private static readonly object _lock = new object();

        public static void Run(TimeSpan? maxRunningDuration = null)
        {
            const int tickRateHz = 20;
            const int entityId = 1;

            // Setup world + engine (no transport)
            World world = new World();
            world.CreateEntityAt(entityId: entityId, x: 0, y: 0);
            ServerEngine engine = new ServerEngine(tickRateHz, world);

            CancellationTokenSource cts;
            lock (_lock)
            {
                _currentCts?.Dispose();
                cts = new CancellationTokenSource();
                _currentCts = cts;
            }

            try
            {
                // Background: input injector (pretend client)
                Thread inputThread = new Thread(() => InputLoop(engine, cts.Token, entityId))
                {
                    IsBackground = true,
                    Name = "AutoEngineTickProgram.Input"
                };
                inputThread.Start();

                // Background: tick loop
                EngineTickResult last = default;
                object lastLock = new object();

                Thread engineThread = new Thread(() =>
                {
                    RunEngineLoop(engine, cts.Token, r =>
                    {
                        lock (lastLock) last = r;
                    });
                })
                {
                    IsBackground = true,
                    Name = "AutoEngineTickProgram.Engine"
                };
                engineThread.Start();

                // Optional: stop-after-duration
                Thread? durationThread = null;
                if (maxRunningDuration.HasValue)
                {
                    durationThread = new Thread(() =>
                    {
                        Thread.Sleep(maxRunningDuration.Value);
                        Stop();
                    })
                    {
                        IsBackground = true,
                        Name = "AutoEngineTickProgram.Duration"
                    };
                    durationThread.Start();
                }

                // Print snapshot periodically
                Stopwatch sw = Stopwatch.StartNew();
                while (!cts.IsCancellationRequested)
                {
                    EngineTickResult r;
                    lock (lastLock) r = last;

                    SampleWorldSnapshot snap = r.Snapshot;

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

                    if (maxRunningDuration.HasValue && sw.Elapsed >= maxRunningDuration.Value)
                        break;

                    Thread.Sleep(500);
                }

                Stop();

                engineThread.Join();
                inputThread.Join();
                durationThread?.Join();
            }
            finally
            {
                lock (_lock)
                {
                    if (_currentCts == cts)
                        _currentCts = null;
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

        private static void InputLoop(ServerEngine engine, CancellationToken token, int entityId)
        {
            uint seq = 0;

            while (!token.IsCancellationRequested)
            {
                // Schedule for next server tick to pass validation window.
                int requestedTick = engine.CurrentServerTick + 1;

                var cmds = new List<EngineCommand<EngineCommandType>>(1)
                {
                    new MoveByEngineCommand(entityId: entityId, dx: 1, dy: 0)
                };

                engine.EnqueueCommands(
                    connId: 1,
                    requestedClientTick: requestedTick,
                    clientCmdSeq: seq++,
                    commands: cmds);

                Thread.Sleep(250);
            }
        }

        private static void RunEngineLoop(ServerEngine engine, CancellationToken token, Action<EngineTickResult> onTick)
        {
            double tickMs = 1000.0 / engine.TickRateHz;

            long freq = Stopwatch.Frequency;
            long next = Stopwatch.GetTimestamp();

            while (!token.IsCancellationRequested)
            {
                EngineTickResult r = engine.TickOnce();
                onTick(r);

                // Drift-corrected fixed-rate loop
                next += (long)(freq * (tickMs / 1000.0));
                while (true)
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

        private static bool TryGetEntityPos(SampleWorldSnapshot snap, int entityId, out int x, out int y)
        {
            SampleEntityPos[] arr = snap.Entities;
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
