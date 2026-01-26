using System;

namespace Sim
{
    /// <summary>
    /// Immutable per-tick context created by the TickRunner and consumed by the engine/systems.
    /// Avoid using ServerTimeMs for deterministic simulation logic.
    /// </summary>
    public readonly struct TickContext
    {
        public readonly int Tick;
        public readonly int TickRateHz;
        public readonly double TickDurationMs;
        public readonly long ServerTimeMs;

        public TickContext(int tick, int tickRateHz, double tickDurationMs, long serverTimeMs)
        {
            Tick = tick;
            TickRateHz = tickRateHz;
            TickDurationMs = tickDurationMs;
            ServerTimeMs = serverTimeMs;
        }
    }
}
