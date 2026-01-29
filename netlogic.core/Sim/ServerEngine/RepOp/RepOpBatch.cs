using System;
using System.Buffers;

namespace com.aqua.netlogic.sim.serverengine
{
    public readonly struct RepOpBatch : IDisposable
    {
        private readonly RepOp[]? _buffer;
        private readonly int _count;
        private readonly bool _pooled;

        public static RepOpBatch Empty => new RepOpBatch(null, 0, pooled: false);

        public int Count => _count;

        public ReadOnlySpan<RepOp> Span => (_buffer == null || _count == 0)
            ? ReadOnlySpan<RepOp>.Empty
            : _buffer.AsSpan(0, _count);

        private RepOpBatch(RepOp[]? buffer, int count, bool pooled)
        {
            _buffer = buffer;
            _count = count;
            _pooled = pooled;
        }

        public static RepOpBatch FromPooled(RepOp[] buffer, int count)
        {
            if (buffer == null || count <= 0)
                return Empty;
            return new RepOpBatch(buffer, count, pooled: true);
        }

        public static RepOpBatch FromOwnedArray(RepOp[] buffer, int count)
        {
            if (buffer == null || count <= 0)
                return Empty;
            return new RepOpBatch(buffer, count, pooled: false);
        }

        public RepOp[] ToArray()
        {
            if (_buffer == null || _count == 0)
                return [];

            RepOp[] copy = new RepOp[_count];
            Array.Copy(_buffer, 0, copy, 0, _count);
            return copy;
        }

        public void Dispose()
        {
            if (_pooled && _buffer != null)
                ArrayPool<RepOp>.Shared.Return(_buffer, clearArray: false);
        }
    }
}
