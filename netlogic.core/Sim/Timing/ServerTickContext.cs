namespace com.aqua.netlogic.sim.timing
{
    /// <summary>
    /// Immutable per-tick context created by the ServerTickRunner and consumed by the engine/systems.
    /// Avoid using ServerTimeMs for deterministic simulation logic.
    /// </summary>
    public readonly struct ServerTickContext(double serverTimeMs, double deltaMs)
    {
        public readonly double ServerTimeMs = serverTimeMs;
        public readonly double DeltaMs = deltaMs;
    }
}
