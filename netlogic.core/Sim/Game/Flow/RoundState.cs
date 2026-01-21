namespace Game
{
    /// <summary>
    /// Server-authoritative round phase.
    /// Keep this minimal: presentation/animation states are client-only.
    /// </summary>
    public enum RoundState : byte
    {
        None = 0,

        /// <summary>
        /// Player is preparing the cook: drafting, arranging, etc.
        /// ClickCook is valid here.
        /// </summary>
        Prepare = 1,

        /// <summary>
        /// Server has computed results and is waiting for player to Continue.
        /// </summary>
        OutcomeReady = 2
    }
}
