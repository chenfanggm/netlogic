using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.game.snapshot;

namespace com.aqua.netlogic.program
{
    /// <summary>
    /// Debug/harness payload containing tick result + snapshot.
    /// </summary>
    public readonly struct TickSnapshot
    {
        public readonly TickResult Result;
        public readonly GameSnapshot Snapshot;

        public TickSnapshot(TickResult result, GameSnapshot snapshot)
        {
            Result = result;
            Snapshot = snapshot;
        }
    }
}
