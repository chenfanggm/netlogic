using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.program
{
    /// <summary>
    /// Simulates the render layer without drawing. It tracks flow transitions
    /// and timing flags that would normally be used by UI state.
    /// </summary>
    internal sealed class RenderSimulator
    {
        public uint ClientCmdSeq;
        public GameFlowState LastClientFlowState;
        public bool ExitingInRound;
        public double ExitMenuAtMs;
        public double ExitAfterVictoryAtMs;
        public double LastPrintAtMs;

        public bool FlowStateChangedThisTick;
        public bool LeftInRoundThisTick;
        public bool EnteredMainMenuAfterVictoryThisTick;

        public void ResetFlowFlags()
        {
            FlowStateChangedThisTick = false;
            LeftInRoundThisTick = false;
            EnteredMainMenuAfterVictoryThisTick = false;
        }
    }
}
