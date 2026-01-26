using System;

namespace Sim.Time
{
    /// <summary>
    /// Immutable per-tick context created by the TickRunner and consumed by the engine/systems.
    /// Avoid using ServerTimeMs for deterministic simulation logic.
    /// </summary>
    public readonly struct TickContext(double serverTimeMs, double elapsedMsSinceLastTick)
    {
        public readonly double ServerTimeMs = serverTimeMs;
        public readonly double ElapsedMsSinceLastTick = elapsedMsSinceLastTick;
    }
}
