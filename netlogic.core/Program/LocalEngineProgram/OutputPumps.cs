using Sim.Engine;

namespace Program
{
    public interface IOutputPump
    {
        void Run(LatestValue<TickSnapshot> latest, CancellationToken token);
    }

    /// <summary>
    /// Console output loop.
    /// Responsibility boundary:
    /// - Reads latest snapshot and prints it (no ticking, no command injection).
    /// </summary>
    public sealed class ConsoleSnapshotOutput(int entityId, TimeSpan period, ISnapshotFormatter formatter) : IOutputPump
    {
        private readonly int _entityId = entityId;
        private readonly TimeSpan _period = period;
        private readonly ISnapshotFormatter _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

        public void Run(LatestValue<TickSnapshot> latest, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TickSnapshot? r = latest.TryRead();
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
