namespace Sim.Game
{
    /// <summary>
    /// Deterministic hash of authoritative game state.
    /// Used for desync detection and replay verification.
    /// Keep stable across versions if you want backward-compatible replays.
    /// </summary>
    public static class WorldHash
    {
        // FNV-1a 32-bit
        public static uint Compute(Game world)
        {
            uint h = 2166136261u;

            foreach (Entity e in world.Entities)
            {
                h = Mix(h, (uint)e.Id);
                h = Mix(h, (uint)e.X);
                h = Mix(h, (uint)e.Y);
                h = Mix(h, (uint)e.Hp);
            }

            return h;
        }

        private static uint Mix(uint h, uint v)
        {
            h ^= v;
            h *= 16777619u;
            return h;
        }
    }
}
