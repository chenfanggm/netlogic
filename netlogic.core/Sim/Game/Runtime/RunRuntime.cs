
namespace com.aqua.netlogic.sim.game.runtime
{
    /// <summary>
    /// Authoritative run-wide persistent state.
    /// </summary>
    public sealed class RunRuntime
    {
        /// <summary>
        /// 0 = none selected.
        /// </summary>
        public int SelectedChefHatId { get; internal set; }

        /// <summary>
        /// Starts from 1.
        /// </summary>
        public int LevelIndex { get; internal set; }

        /// <summary>
        /// Seed chosen at run start. Deterministic.
        /// </summary>
        public uint RunSeed { get; internal set; }

        /// <summary>
        /// Deterministic RNG state for run.
        /// </summary>
        public DeterministicRng Rng { get; internal set; }
    }
}
