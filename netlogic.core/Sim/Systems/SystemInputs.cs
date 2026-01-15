namespace Sim.Systems
{
    /// <summary>
    /// Provides systems access to their routed commands for the current tick.
    /// </summary>
    public readonly struct SystemInputs
    {
        public readonly int Tick;
        public readonly CommandRouter Router;

        public SystemInputs(int tick, CommandRouter router)
        {
            Tick = tick;
            Router = router;
        }
    }
}
