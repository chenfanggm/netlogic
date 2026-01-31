using System.Collections.Generic;

namespace com.aqua.netlogic.sim.game.runtime
{
    /// <summary>
    /// Authoritative level-wide state. Each level has 3 rounds/customers.
    /// Refresh pool is shared across the level.
    /// </summary>
    public sealed class LevelRuntime
    {
        private readonly int[] _customerIds = new int[3];
        private readonly bool[] _served = new bool[3];

        /// <summary>
        /// Remaining pantry refreshes for this level (shared across 3 rounds).
        /// </summary>
        public int RefreshesRemaining { get; internal set; }

        /// <summary>
        /// 3 customers previewed for the level (like Balatro blinds).
        /// Using IDs here; the actual customer definitions live in data tables.
        /// </summary>
        public IReadOnlyList<int> CustomerIds => _customerIds;
        internal int[] CustomerIdsMutable => _customerIds;

        /// <summary>
        /// Whether each of the 3 customers has been served.
        /// </summary>
        public IReadOnlyList<bool> Served => _served;
        internal bool[] ServedMutable => _served;

        /// <summary>
        /// When player clicks "Serve" we store the chosen index here for deterministic initialization.
        /// -1 means none.
        /// </summary>
        public int PendingServeCustomerIndex { get; internal set; } = -1;

        public void ResetForNewLevel()
        {
            RefreshesRemaining = 0;
            for (int i = 0; i < _customerIds.Length; i++)
                _customerIds[i] = 0;
            for (int i = 0; i < _served.Length; i++)
                _served[i] = false;

            PendingServeCustomerIndex = -1;
        }

        public int ServedCount()
        {
            int c = 0;
            for (int i = 0; i < _served.Length; i++)
                if (_served[i]) c++;
            return c;
        }
    }
}
