namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Client model uses op type to construct its own state (NOT internal domain events).
    /// Keep stable; extend conservatively.
    /// </summary>
    public enum RepOpType : byte
    {
        None = 0,

        // Reliable lane (authoritative flow/UI control)
        FlowFire = 10,
        FlowSnapshot = 11,

        // Reliable lane (entity lifecycle)
        EntitySpawned = 20,
        EntityDestroyed = 21,

        // Unreliable state snapshot (presentation only).
        // Latest-wins; safe to drop or overwrite.
        // MUST NOT affect simulation truth.
        PositionSnapshot = 50,
    }
}

