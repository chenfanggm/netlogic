using System;
using System.Buffers;
using System.Collections.Generic;

namespace Sim.Engine
{
    internal static class RepOpPartitioner
    {
        // If you ever want deterministic sample ordering for tests/replays, flip this to true.
        // Usually not required (sample lane = latest state).
        private const bool StableSampleOrder = false;

        internal readonly struct PartitionedOps : IDisposable
        {
            private readonly RepOp[]? _reliableBuf;
            private readonly int _reliableCount;

            private readonly RepOp[]? _sampleBuf;
            private readonly int _sampleCount;

            public ReadOnlySpan<RepOp> Reliable => _reliableBuf == null ? ReadOnlySpan<RepOp>.Empty : _reliableBuf.AsSpan(0, _reliableCount);
            public ReadOnlySpan<RepOp> Sample => _sampleBuf == null ? ReadOnlySpan<RepOp>.Empty : _sampleBuf.AsSpan(0, _sampleCount);

            public PartitionedOps(RepOp[]? reliableBuf, int reliableCount, RepOp[]? sampleBuf, int sampleCount)
            {
                _reliableBuf = reliableBuf;
                _reliableCount = reliableCount;
                _sampleBuf = sampleBuf;
                _sampleCount = sampleCount;
            }

            public void Dispose()
            {
                if (_reliableBuf != null) ArrayPool<RepOp>.Shared.Return(_reliableBuf, clearArray: false);
                if (_sampleBuf != null) ArrayPool<RepOp>.Shared.Return(_sampleBuf, clearArray: false);
            }
        }

        /// <summary>
        /// Partition ops:
        /// - Reliable: appended in input order
        /// - Sample: coalesced by (type, replaceKey) within this tick (latest-wins)
        /// </summary>
        public static PartitionedOps Partition(ReadOnlySpan<RepOp> ops)
        {
            if (ops.Length == 0)
                return new PartitionedOps(null, 0, null, 0);

            RepOp[] reliable = ArrayPool<RepOp>.Shared.Rent(ops.Length);
            int reliableCount = 0;

            Dictionary<long, RepOp>? sampleMap = null;

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];

                var delivery = RepOpDeliveryPolicy.DeliveryOf(op.Type);
                if (delivery == RepOpDeliveryPolicy.Delivery.Reliable)
                {
                    reliable[reliableCount++] = op;
                    continue;
                }

                sampleMap ??= new Dictionary<long, RepOp>(64);

                int replaceKey = RepOpDeliveryPolicy.SampleReplaceKey(in op);
                long key = MakeSampleKey(op.Type, replaceKey);

                // latest-wins within this tick
                sampleMap[key] = op;
            }

            if (sampleMap == null || sampleMap.Count == 0)
                return new PartitionedOps(reliable, reliableCount, null, 0);

            RepOp[] sample = ArrayPool<RepOp>.Shared.Rent(sampleMap.Count);
            int sampleCount = 0;
            foreach (var kv in sampleMap)
                sample[sampleCount++] = kv.Value;

            if (StableSampleOrder && sampleCount > 1)
            {
                Array.Sort(sample, 0, sampleCount, SampleOpComparer.Instance);
            }

            return new PartitionedOps(reliable, reliableCount, sample, sampleCount);
        }

        private static long MakeSampleKey(RepOpType type, int replaceKey)
            => ((long)(int)type << 32) | (uint)replaceKey;

        private sealed class SampleOpComparer : IComparer<RepOp>
        {
            public static readonly SampleOpComparer Instance = new SampleOpComparer();

            public int Compare(RepOp x, RepOp y)
            {
                int t = x.Type.CompareTo(y.Type);
                if (t != 0) return t;

                // sample coalesce key for stable ordering
                int kx = RepOpDeliveryPolicy.SampleReplaceKey(in x);
                int ky = RepOpDeliveryPolicy.SampleReplaceKey(in y);
                return kx.CompareTo(ky);
            }
        }
    }
}
