using System.Diagnostics;

namespace Sim
{
    public sealed class TickClock
    {
        public int TickRateHz { get; }
        public double TickDurationMs { get; }

        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private double _nextTickAtMs;

        public TickClock(int tickRateHz)
        {
            TickRateHz = tickRateHz;
            TickDurationMs = 1000.0 / tickRateHz;
            _nextTickAtMs = TickDurationMs;
        }

        // Blocks until next tick deadline. For practice, blocking is fine.
        public void WaitForNextTick()
        {
            while (true)
            {
                double elapsedMs = _sw.ElapsedMilliseconds;
                double remainingMs = _nextTickAtMs - elapsedMs;

                if (remainingMs <= 0)
                    break;

                // Sleep a bit (coarse) then spin briefly.
                if (remainingMs > 2.0)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(100);
            }

            _nextTickAtMs += TickDurationMs;
        }
    }
}
