namespace com.aqua.netlogic.sim.game.runtime
{
    /// <summary>
    /// Tiny deterministic RNG suitable for server simulation.
    /// Do NOT use System.Random for lockstep determinism across runtimes.
    /// </summary>
    public struct DeterministicRng
    {
        // LCG: x = (a*x + c) mod 2^32
        private uint _state;

        public DeterministicRng(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public uint State => _state;

        public uint NextU32()
        {
            unchecked
            {
                _state = _state * 1664525u + 1013904223u;
                return _state;
            }
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            uint r = NextU32();
            uint range = (uint)(maxExclusive - minInclusive);
            return (int)(r % range) + minInclusive;
        }
    }
}
