using Game.Runtime;

namespace Game
{
    /// <summary>
    /// Authoritative run-wide persistent state.
    /// </summary>
    public sealed class RunRuntime
    {
        /// <summary>
        /// 0 = none selected.
        /// </summary>
        public int SelectedChefHatId;

        /// <summary>
        /// Starts from 1.
        /// </summary>
        public int LevelIndex;

        /// <summary>
        /// Seed chosen at run start. Deterministic.
        /// </summary>
        public uint RunSeed;

        /// <summary>
        /// Deterministic RNG state for run.
        /// </summary>
        public DeterministicRng Rng;
    }
}
