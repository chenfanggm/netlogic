using System;
using System.Diagnostics;

namespace Sim
{
    /// <summary>
    /// Drives a fixed-tick simulation using real time.
    /// - Calls pollAction frequently
    /// - Calls tickAction at fixed rate, with limited catch-up
    /// This is server/host-loop infrastructure, not game logic.
    /// </summary>
    public sealed class FixedTickRunner
    {
        private readonly int _tickRateHz;
        private readonly double _tickMs;

        private readonly Stopwatch _time;

        private double _accumulatedMs;

        // Prevent spiral-of-death if sim falls behind
        private readonly int _maxTicksPerUpdate;

        public FixedTickRunner(int tickRateHz, int maxTicksPerUpdate)
        {
            if (tickRateHz <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRateHz));
            if (maxTicksPerUpdate <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerUpdate));

            _tickRateHz = tickRateHz;
            _tickMs = 1000.0 / (double)tickRateHz;

            _time = Stopwatch.StartNew();
            _accumulatedMs = 0;

            _maxTicksPerUpdate = maxTicksPerUpdate;
        }

        public void Step(Action pollAction, Action tickAction)
        {
            if (pollAction == null)
                throw new ArgumentNullException(nameof(pollAction));
            if (tickAction == null)
                throw new ArgumentNullException(nameof(tickAction));

            // Accumulate elapsed time since last step
            double elapsedMs = _time.Elapsed.TotalMilliseconds;
            _time.Restart();

            _accumulatedMs += elapsedMs;

            // Always poll first (receive input, connect/disconnect, etc.)
            pollAction();

            int ticks = 0;
            while (_accumulatedMs >= _tickMs && ticks < _maxTicksPerUpdate)
            {
                tickAction();
                ticks++;
                _accumulatedMs -= _tickMs;

                // Poll between ticks so receive queue doesn't starve
                pollAction();
            }

            // If we are massively behind, drop excess time (hard catch-up cap)
            if (_accumulatedMs > _tickMs * _maxTicksPerUpdate)
            {
                _accumulatedMs = 0;
            }
        }
    }
}
