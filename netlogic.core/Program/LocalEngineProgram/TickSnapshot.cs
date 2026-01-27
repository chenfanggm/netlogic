using Sim.Engine;
using Sim.Snapshot;

namespace Program
{
    /// <summary>
    /// Debug/harness payload containing tick frame + snapshot.
    /// </summary>
    public readonly struct TickSnapshot
    {
        public readonly TickFrame Frame;
        public readonly GameSnapshot Snapshot;

        public TickSnapshot(TickFrame frame, GameSnapshot snapshot)
        {
            Frame = frame;
            Snapshot = snapshot;
        }
    }
}
