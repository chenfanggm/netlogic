namespace com.aqua.netlogic.sim.serverengine
{
    public enum EngineCommandType : byte
    {
        None = 0,
        // MovementSystem
        MoveBy = 1,
        Dash = 2,
        GrantHaste = 3,

        // GameFlowSystem
        FlowFire = 10
    }
}
