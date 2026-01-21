using Sim;

namespace Game
{
    /// <summary>
    /// Sample snapshot payload sent each server tick (or at sampling frequency).
    /// Combines flow runtime + entity positions.
    /// </summary>
    public sealed class SampleWorldSnapshot
    {
        public readonly FlowSnapshot Flow;
        public readonly SampleEntityPos[] Entities;

        public SampleWorldSnapshot(FlowSnapshot flow, SampleEntityPos[] entities)
        {
            Flow = flow;
            Entities = entities;
        }
    }
}
