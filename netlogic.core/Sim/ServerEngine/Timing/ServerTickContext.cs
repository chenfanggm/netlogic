namespace com.aqua.netlogic.sim.timing
{
    /// <summary>
    /// Immutable per-tick context created by the ServerTickRunner and consumed by the engine/systems.
    /// Avoid using NowMs for deterministic simulation logic.
    /// </summary>
    public readonly struct ServerTickContext(double nowMs, double deltaMs)
    {
        public readonly double NowMs = nowMs;
        public readonly double DeltaMs = deltaMs;
    }
}
