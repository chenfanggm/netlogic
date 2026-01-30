using com.aqua.netlogic.sim.game.flow;

namespace com.aqua.netlogic.program
{
    /// <summary>
    /// Simulates the render layer without drawing. It tracks flow transitions
    /// and timing flags that would normally be used by UI state.
    /// </summary>
    public sealed class RenderSimulator
    {
        public double ExitAfterVictoryAtMs;
        public double LastServerTimeMs;
    }
}
