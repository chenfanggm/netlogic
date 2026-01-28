using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.game
{
    /// <summary>
    /// Transient per-tick output hook, set by the engine.
    /// Systems can record replication ops through the world without knowing transports.
    /// This is NOT serialized as part of the game state.
    /// </summary>
    internal interface IWorldReplicator
    {
        void Record(in RepOp op);
    }

    internal sealed class WorldReplicator : IWorldReplicator
    {
        private readonly IReplicationRecorder _rec;

        public WorldReplicator(IReplicationRecorder recorder)
        {
            _rec = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        public void Record(in RepOp op)
        {
            _rec.Record(op);
        }
    }
}
