using System;
using System.Buffers;
using System.Collections.Generic;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.serverengine.replication
{
    internal static class RepOpPartitioner
    {
        // If you ever want deterministic unreliable ordering for tests/replays, flip this to true.
        // Usually not required (unreliable lane = latest state).
        private const bool StableUnreliableOrder = false;

        internal readonly struct PartitionedOps : IDisposable
        {
            private readonly RepOp[]? _reliableBuf;
            private readonly int _reliableCount;

            private readonly RepOp[]? _unreliableBuf;
            private readonly int _unreliableCount;

            public ReadOnlySpan<RepOp> Reliable => _reliableBuf == null ? ReadOnlySpan<RepOp>.Empty : _reliableBuf.AsSpan(0, _reliableCount);
            public ReadOnlySpan<RepOp> Unreliable => _unreliableBuf == null ? ReadOnlySpan<RepOp>.Empty : _unreliableBuf.AsSpan(0, _unreliableCount);

            public PartitionedOps(RepOp[]? reliableBuf, int reliableCount, RepOp[]? unreliableBuf, int unreliableCount)
            {
                _reliableBuf = reliableBuf;
                _reliableCount = reliableCount;
                _unreliableBuf = unreliableBuf;
                _unreliableCount = unreliableCount;
            }

            public void Dispose()
            {
                if (_reliableBuf != null) ArrayPool<RepOp>.Shared.Return(_reliableBuf, clearArray: false);
                if (_unreliableBuf != null) ArrayPool<RepOp>.Shared.Return(_unreliableBuf, clearArray: false);
            }
        }

        /// <summary>
        /// Partition ops:
        /// - Reliable: appended in input order
        /// - Unreliable: coalesced by (type, replaceKey) within this tick (latest-wins)
        /// </summary>
        public static PartitionedOps Partition(ReadOnlySpan<RepOp> ops)
        {
            if (ops.Length == 0)
                return new PartitionedOps(null, 0, null, 0);

            RepOp[] reliable = ArrayPool<RepOp>.Shared.Rent(ops.Length);
            int reliableCount = 0;

            Dictionary<long, RepOp>? unreliableMap = null;

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];

                var delivery = RepOpDeliveryPolicy.DeliveryOf(op.Type);
                if (delivery == RepOpDeliveryPolicy.Delivery.Reliable)
                {
                    reliable[reliableCount++] = op;
                    continue;
                }

                unreliableMap ??= new Dictionary<long, RepOp>(64);

                int replaceKey = RepOpDeliveryPolicy.UnreliableReplaceKey(in op);
                long key = MakeUnreliableKey(op.Type, replaceKey);

                // latest-wins within this tick
                unreliableMap[key] = op;
            }

            if (unreliableMap == null || unreliableMap.Count == 0)
                return new PartitionedOps(reliable, reliableCount, null, 0);

            RepOp[] unreliable = ArrayPool<RepOp>.Shared.Rent(unreliableMap.Count);
            int unreliableCount = 0;
            foreach (var kv in unreliableMap)
                unreliable[unreliableCount++] = kv.Value;

            if (StableUnreliableOrder && unreliableCount > 1)
            {
                Array.Sort(unreliable, 0, unreliableCount, UnreliableOpComparer.Instance);
            }

            return new PartitionedOps(reliable, reliableCount, unreliable, unreliableCount);
        }

        private static long MakeUnreliableKey(RepOpType type, int replaceKey)
            => ((long)(int)type << 32) | (uint)replaceKey;

        private sealed class UnreliableOpComparer : IComparer<RepOp>
        {
            public static readonly UnreliableOpComparer Instance = new UnreliableOpComparer();

            public int Compare(RepOp x, RepOp y)
            {
                int t = x.Type.CompareTo(y.Type);
                if (t != 0) return t;

                // unreliable coalesce key for stable ordering
                int kx = RepOpDeliveryPolicy.UnreliableReplaceKey(in x);
                int ky = RepOpDeliveryPolicy.UnreliableReplaceKey(in y);
                return kx.CompareTo(ky);
            }
        }
    }
}
