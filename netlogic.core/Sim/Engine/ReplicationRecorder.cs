using System;
using System.Buffers;

namespace Sim.Engine
{
    public interface IReplicationRecorder
    {
        void BeginTick(int tick);
        void Record(in RepOp op);
        RepOpBatch EndTickAndFlush();
    }

    /// <summary>
    /// Per-tick op buffer using ArrayPool to avoid per-tick ToArray() allocations.
    /// NOTE: EndTickAndFlush hands off the current buffer; caller must Dispose the batch.
    /// </summary>
    public sealed class ReplicationRecorder : IReplicationRecorder
    {
        private RepOp[] _buf;
        private int _count;

        public ReplicationRecorder(int initialCapacity = 128)
        {
            int cap = Math.Max(8, initialCapacity);
            _buf = ArrayPool<RepOp>.Shared.Rent(cap);
            _count = 0;
        }

        public void BeginTick(int tick)
        {
            _ = tick;
            _count = 0;
        }

        public void Record(in RepOp op)
        {
            if (_count >= _buf.Length)
                Grow();

            _buf[_count++] = op;
        }

        public RepOpBatch EndTickAndFlush()
        {
            if (_count == 0)
                return RepOpBatch.Empty;

            // Hand off current buffer and immediately rent a new one for next tick.
            RepOp[] handed = _buf;
            int count = _count;

            _buf = ArrayPool<RepOp>.Shared.Rent(handed.Length);
            _count = 0;

            return RepOpBatch.FromPooled(handed, count);
        }

        private void Grow()
        {
            int newCap = Math.Max(8, _buf.Length * 2);
            RepOp[] next = ArrayPool<RepOp>.Shared.Rent(newCap);

            Array.Copy(_buf, 0, next, 0, _count);
            ArrayPool<RepOp>.Shared.Return(_buf, clearArray: false);

            _buf = next;
        }
    }
}
