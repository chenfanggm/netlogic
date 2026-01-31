using com.aqua.netlogic.sim.game;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Server-side op sink used by command handlers/systems.
    /// Supports EmitAndApply so later decisions see updated model state.
    ///
    /// IMPORTANT:
    /// - Tick is context (NOT authoritative state). Do not store tick on ServerModel.
    /// - Any rule that derives ops from "current tick" must read OpWriter.Tick.
    /// </summary>
    public sealed class OpWriter
    {
        private readonly IReplicationRecorder _recorder;
        private readonly ServerModel _serverModel;

        public int Tick { get; }

        public OpWriter(ServerModel serverModel, IReplicationRecorder recorder, int tick)
        {
            _serverModel = serverModel;
            _recorder = recorder;
            Tick = tick;
        }

        public void Emit(in RepOp op) => _recorder.Record(op);

        public void EmitAndApply(in RepOp op)
        {
            _recorder.Record(op);
            RepOpApplier.ApplyAuthoritative(_serverModel, op);
        }
    }
}
