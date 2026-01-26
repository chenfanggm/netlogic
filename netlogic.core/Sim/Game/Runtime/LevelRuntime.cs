namespace Sim.Game.Runtime
{
    /// <summary>
    /// Authoritative level-wide state. Each level has 3 rounds/customers.
    /// Refresh pool is shared across the level.
    /// </summary>
    public sealed class LevelRuntime
    {
        /// <summary>
        /// Remaining pantry refreshes for this level (shared across 3 rounds).
        /// </summary>
        public int RefreshesRemaining;

        /// <summary>
        /// 3 customers previewed for the level (like Balatro blinds).
        /// Using IDs here; the actual customer definitions live in data tables.
        /// </summary>
        public int[] CustomerIds = new int[3];

        /// <summary>
        /// Whether each of the 3 customers has been served.
        /// </summary>
        public bool[] Served = new bool[3];

        /// <summary>
        /// When player clicks "Serve" we store the chosen index here for deterministic initialization.
        /// -1 means none.
        /// </summary>
        public int PendingServeCustomerIndex = -1;

        public void ResetForNewLevel()
        {
            RefreshesRemaining = 0;
            for (int i = 0; i < CustomerIds.Length; i++)
                CustomerIds[i] = 0;
            for (int i = 0; i < Served.Length; i++)
                Served[i] = false;

            PendingServeCustomerIndex = -1;
        }

        public int ServedCount()
        {
            int c = 0;
            for (int i = 0; i < Served.Length; i++)
                if (Served[i]) c++;
            return c;
        }
    }
}
