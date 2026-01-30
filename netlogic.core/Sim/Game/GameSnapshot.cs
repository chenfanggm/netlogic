using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.game.snapshot
{
    /// <summary>
    /// Unreliable snapshot payload sent each server tick (or at sampling frequency).
    /// Combines flow runtime + entity positions.
    /// </summary>
    public sealed class GameSnapshot(FlowSnapshot flow, SampleEntityPos[] entities, int serverTick, uint stateHash)
    {
        public readonly FlowSnapshot Flow = flow;
        public readonly SampleEntityPos[] Entities = entities;
        public readonly int ServerTick = serverTick;
        public readonly uint StateHash = stateHash;
    }
}
