namespace com.aqua.netlogic.net
{
    /// <summary>
    /// Hash contract/versioning so clients never compare incompatible hashes.
    /// Bump ScopeId whenever you change WorldHash.Compute() inputs/ordering.
    /// </summary>
    public static class HashContract
    {
        // Change this if you change WorldHash.Compute().
        public const ushort ScopeId = 1;

        public enum HashPhase : byte
        {
            PreTick = 0,
            PostTick = 1
        }

        // We standardize on PostTick hashes: hash of authoritative state AFTER applying tick commands/systems.
        public const HashPhase Phase = HashPhase.PostTick;
    }
}
