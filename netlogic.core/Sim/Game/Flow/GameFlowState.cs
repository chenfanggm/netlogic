namespace Game
{
    /// <summary>
    /// Top-level game flow states for Coquus.
    /// Persisted in World, deterministic.
    /// </summary>
    public enum GameFlowState : byte
    {
        Boot = 0,

        // After boot: "New Game"
        MainMenu = 1,

        // MERGED: hat selection + start confirmation
        RunSetup = 2,

        // Inside run: show 3 customers like Balatro blinds
        LevelOverview = 3,

        // Round is a sub-state machine (Prepare/Resolving/Outcome) but top-level is "InRound"
        InRound = 4,

        RunDefeat = 5,
        RunVictory = 6
    }
}
