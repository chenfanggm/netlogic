using System;
using System.Diagnostics;
using System.Threading;

namespace Sim
{
    /// <summary>
    /// Realtime scheduling loop driver.
    /// Owns Stopwatch/deadlines and decides when to invoke the tick callback.
    /// Does NOT own or mutate authoritative simulation tick counters.
    /// </summary>
    public sealed class TickRunner
    {
        public int TickRateHz { get; }
        public double TickDurationMs { get; }

        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private double _nextTickAtMs;

        // Prevent death spirals if server falls behind.
        private readonly int _maxCatchUpTicksPerLoop;

        public TickRunner(int tickRateHz, int maxCatchUpTicksPerLoop = 5)
        {
            if (tickRateHz <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRateHz));
            if (maxCatchUpTicksPerLoop <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCatchUpTicksPerLoop));

            TickRateHz = tickRateHz;
            TickDurationMs = 1000.0 / tickRateHz;
            _nextTickAtMs = TickDurationMs;
            _maxCatchUpTicksPerLoop = maxCatchUpTicksPerLoop;
        }

        public long ServerTimeMs => _sw.ElapsedMilliseconds;

        /// <summary>
        /// Runs until token cancellation. Calls onTick one or more times per loop to catch up.
        /// The callback receives (tickIndexForThisCallback, tickContextServerTimeMs).
        /// Tick index here is runner-local sequencing ONLY; engine should own the authoritative tick.
        /// </summary>
        public void Run(Action<TickContext> onTick, CancellationToken token)
        {
            if (onTick == null)
                throw new ArgumentNullException(nameof(onTick));

            int runnerTick = 0;

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
                while (executed < _maxCatchUpTicksPerLoop &&
                    (_nextTickAtMs - _sw.Elapsed.TotalMilliseconds) <= 0 &&
                    !token.IsCancellationRequested)
                {
                    runnerTick++;
                    long serverTimeMs = _sw.ElapsedMilliseconds;

                    TickContext ctx = new TickContext(
                        tick: runnerTick,
                        tickRateHz: TickRateHz,
                        tickDurationMs: TickDurationMs,
                        serverTimeMs: serverTimeMs);

                    onTick(ctx);

                    CommitNextTick();
                    executed++;
                }
            }
        }

        private void CommitNextTick()
        {
            double nowMs = _sw.Elapsed.TotalMilliseconds;
            double remainingMs = _nextTickAtMs - nowMs;

            if (remainingMs <= 0)
            {
                int skipped = (int)(-remainingMs / TickDurationMs) + 1;
                _nextTickAtMs += skipped * TickDurationMs;
            }
        }
    }
}
