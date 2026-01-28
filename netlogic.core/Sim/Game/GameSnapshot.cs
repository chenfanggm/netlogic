using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.game.snapshot
{
    /// <summary>
    /// Unreliable snapshot payload sent each server tick (or at sampling frequency).
    /// Combines flow runtime + entity positions.
    /// </summary>
    public sealed class GameSnapshot
    {
        public readonly FlowSnapshot Flow;
        public readonly SampleEntityPos[] Entities;

        public GameSnapshot(FlowSnapshot flow, SampleEntityPos[] entities)
        {
            Flow = flow;
            Entities = entities;
        }
    }
}
