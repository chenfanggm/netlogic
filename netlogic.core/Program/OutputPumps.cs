using System;
using System.Threading;
using Sim;

namespace Program
{
    public interface IOutputPump
    {
        void Run(LatestValue<EngineTickResult> latest, CancellationToken token);
    }

    /// <summary>
    /// Console output loop.
    /// Responsibility boundary:
    /// - Reads latest snapshot and prints it (no ticking, no command injection).
    /// </summary>
    public sealed class ConsoleSnapshotOutput : IOutputPump
    {
        private readonly int _entityId;
        private readonly TimeSpan _period;
        private readonly ISnapshotFormatter _formatter;

        public ConsoleSnapshotOutput(int entityId, TimeSpan period, ISnapshotFormatter formatter)
        {
            _entityId = entityId;
            _period = period;
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        public void Run(LatestValue<EngineTickResult> latest, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                EngineTickResult? r = latest.TryRead();
                if (!r.HasValue)
                {
                    Console.WriteLine("Waiting for first tick...");
                }
                else
                {
                    Console.WriteLine(_formatter.Format(r.Value, _entityId));
                }

                SleepRespectingCancel(_period, token);
            }
        }

        private static void SleepRespectingCancel(TimeSpan duration, CancellationToken token)
        {
            const int sliceMs = 25;
            int remaining = (int)duration.TotalMilliseconds;

            while (remaining > 0 && !token.IsCancellationRequested)
            {
                int step = remaining > sliceMs ? sliceMs : remaining;
                Thread.Sleep(step);
                remaining -= step;
            }
        }
    }
}
