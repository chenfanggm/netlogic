namespace Sim.Engine
{
    public interface IReplicationRecorder
    {
        void BeginTick(int tick);
        void Record(in RepOp op);
        RepOp[] EndTickAndFlush();
    }

    /// <summary>
    /// Simple per-tick op buffer. Later you can pool arrays.
    /// </summary>
    public sealed class ReplicationRecorder : IReplicationRecorder
    {
        private readonly List<RepOp> _ops;

        public ReplicationRecorder(int initialCapacity = 128)
        {
            _ops = new List<RepOp>(Math.Max(8, initialCapacity));
        }

        public void BeginTick(int tick)
        {
            _ = tick;
            _ops.Clear();
        }

        public void Record(in RepOp op)
        {
            _ops.Add(op);
        }

        public RepOp[] EndTickAndFlush()
        {
            return _ops.Count == 0 ? Array.Empty<RepOp>() : _ops.ToArray();
        }
    }
}
