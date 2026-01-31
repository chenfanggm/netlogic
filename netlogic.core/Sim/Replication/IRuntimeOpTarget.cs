using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.runtime;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Minimal state containers that flow/runtime ops mutate.
    /// Implemented by ServerModel and ClientModel (after we add runtime containers on client).
    /// </summary>
    internal interface IRuntimeOpTarget
    {
        GameFlowState FlowState { get; set; }

        RunRuntime Run { get; }
        LevelRuntime Level { get; }
        RoundRuntime Round { get; }
    }
}
