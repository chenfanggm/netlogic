using System;
using com.aqua.netlogic.sim.game.snapshot;

namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Baseline payload for client reconstruction.
    /// </summary>
    public readonly struct BaselineResult
    {
        public readonly GameSnapshot Snapshot;

        public int ServerTick => Snapshot.ServerTick;
        public uint StateHash => Snapshot.StateHash;

        public BaselineResult(GameSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }
}
