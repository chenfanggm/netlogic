using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.game.snapshot;

namespace com.aqua.netlogic.program
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
