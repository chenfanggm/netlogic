namespace com.aqua.netlogic.net
{
    /// <summary>
    /// Network packet delivery lane (reliable vs unreliable).
    /// </summary>
    public enum Lane : byte
    {
        Reliable = 1,
        /// <summary>
        /// Unreliable, latest-wins state deltas (e.g. position snapshots).
        /// Must never affect authoritative simulation truth.
        /// </summary>
        Unreliable = 2
    }
}
