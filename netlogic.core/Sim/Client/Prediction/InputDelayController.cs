using System;

namespace Sim.Client
{
    /// <summary>
    /// Adaptive input delay controller that adjusts based on RTT measurements.
    /// </summary>
    public sealed class InputDelayController
    {
        private readonly int _tickRateHz;

        public int InputDelayTicks { get; private set; }
        public int RenderDelayTicks { get; private set; }

        public InputDelayController(int tickRateHz)
        {
            _tickRateHz = tickRateHz;
            InputDelayTicks = 1;
            RenderDelayTicks = 2;
        }

        public void UpdateFromRttMs(double rttMs)
        {
            // one-way latency estimate ~= RTT/2
            double oneWayMs = rttMs * 0.5;
            double ticks = (oneWayMs / 1000.0) * _tickRateHz;

            int delay = (int)Math.Ceiling(ticks) + 1;

            if (delay < 1)
                delay = 1;
            if (delay > 8)
                delay = 8;

            InputDelayTicks = delay;
            RenderDelayTicks = delay + 1;
        }
    }
}
