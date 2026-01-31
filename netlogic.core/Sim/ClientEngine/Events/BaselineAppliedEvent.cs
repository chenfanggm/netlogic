namespace com.aqua.netlogic.sim.clientengine
{
    /// <summary>
    /// Presentation bootstrap hook: emitted when a baseline snapshot is applied.
    /// Useful for UI initialization/rebuild without inventing fake transitions.
    /// </summary>
    public readonly struct BaselineAppliedEvent
    {
        public readonly int ServerTick;
        public readonly uint StateHash;

        public BaselineAppliedEvent(int serverTick, uint stateHash)
        {
            ServerTick = serverTick;
            StateHash = stateHash;
        }
    }
}
