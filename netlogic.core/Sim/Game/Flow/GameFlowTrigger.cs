namespace Game
{
    /// <summary>
    /// Player intents that can change game flow.
    /// These are the ONLY flow-related values that should come in as EngineCommands.
    /// </summary>
    public enum GameFlowIntent : byte
    {
        None = 0,

        // Main menu
        ClickNewGame = 1,

        // Run setup
        SelectChefHat = 10,     // payload: HatId
        ClickStartRun = 11,     // requires Hat selected (validated in handler/controller)

        // Level overview
        ClickServeCustomer = 20, // payload: CustomerIndex (0..2)

        // Round
        ClickCook = 30,          // commit a cook attempt
        ClickContinue = 31,      // optional: acknowledge outcome UI and proceed

        // Exit / debug / concede
        ClickConcedeRun = 40,
        ReturnToMenu = 41
    }
}
