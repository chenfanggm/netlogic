using com.aqua.netlogic.sim.game;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Server-side op sink used by command handlers/systems.
    /// Supports EmitAndApply so later decisions see updated model state.
    /// </summary>
    public sealed class OpWriter
    {
        private readonly IReplicationRecorder _recorder;
        private readonly ServerModel _serverModel;

        public OpWriter(ServerModel serverModel, IReplicationRecorder recorder)
        {
            _serverModel = serverModel;
            _recorder = recorder;
        }

        public void Emit(in RepOp op) => _recorder.Record(op);

        public void EmitAndApply(in RepOp op)
        {
            _recorder.Record(op);
            RepOpApplier.Apply(_serverModel, op);
        }
    }
}
