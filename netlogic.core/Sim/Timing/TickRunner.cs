using System.Diagnostics;

namespace Sim.Time
{
    /// <summary>
    /// Realtime scheduling loop driver.
    /// Owns Stopwatch/deadlines and decides when to invoke the tick callback.
    /// </summary>
    public sealed class TickRunner
    {
        public int TickRateHz { get; }
        public double TickDurationMs { get; }
        public long ServerTimeMs => _sw.ElapsedMilliseconds;

        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private double _nextTickAtMs;
        private double _lastTickAtMs;

        // Prevent death spirals if server falls behind.
        private readonly int _maxCatchUpTicksPerLoop;

        public TickRunner(int tickRateHz, int maxCatchUpTicksPerLoop = 5)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickRateHz);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCatchUpTicksPerLoop);

            TickRateHz = tickRateHz;
            TickDurationMs = 1000.0 / tickRateHz;
            _nextTickAtMs = TickDurationMs;
            _maxCatchUpTicksPerLoop = maxCatchUpTicksPerLoop;
        }

        /// <summary>
        /// Runs until token cancellation. Calls onTick one or more times per loop to catch up.
        /// The callback receives TickContext with timing data only.
        /// </summary>
        public void Run(Action<TickContext> onTick, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(onTick);

            _lastTickAtMs = _sw.Elapsed.TotalMilliseconds;

            while (!token.IsCancellationRequested)
            {
                double remainingMs = _nextTickAtMs - _sw.Elapsed.TotalMilliseconds;
                if (remainingMs > 0)
                {
                    int sleepMs = (int)Math.Min(remainingMs, 5);
                    if (sleepMs > 0)
                        Thread.Sleep(sleepMs);
                    else
                        Thread.Yield();

                    continue;
                }

                int executed = 0;
                double remainingMsAfterWait = _nextTickAtMs - _sw.Elapsed.TotalMilliseconds;
                while (executed < _maxCatchUpTicksPerLoop && remainingMsAfterWait <= 0 && !token.IsCancellationRequested)
                {
                    double nowMs = _sw.Elapsed.TotalMilliseconds;
                    double elapsedSinceLastTickMs = nowMs - _lastTickAtMs;
                    onTick(new TickContext(nowMs, elapsedSinceLastTickMs));
                    _lastTickAtMs = nowMs;
                    CommitNextTick();
                    executed++;
                }
            }
        }

        private void CommitNextTick()
        {
            double remainingMs = _nextTickAtMs - _sw.Elapsed.TotalMilliseconds;

            if (remainingMs <= 0)
            {
                int skipped = (int)(-remainingMs / TickDurationMs) + 1;
                _nextTickAtMs += skipped * TickDurationMs;
            }
        }
    }
}
