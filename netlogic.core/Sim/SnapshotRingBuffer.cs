using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    /// <summary>
    /// Ring buffer that stores snapshots by tick and provides interpolation pairs for smooth rendering.
    /// </summary>
    public sealed class SnapshotRingBuffer
    {
        private readonly int _capacity;
        private readonly Dictionary<int, SnapshotMsg> _byTick;
        private readonly Queue<int> _order;

        public SnapshotRingBuffer(int capacity)
        {
            _capacity = capacity;
            _byTick = new Dictionary<int, SnapshotMsg>(capacity);
            _order = new Queue<int>(capacity);
        }

        public void Add(SnapshotMsg snapshot)
        {
            int tick = snapshot.Tick;
            if (_byTick.ContainsKey(tick))
                return;

            _byTick[tick] = snapshot;
            _order.Enqueue(tick);

            while (_order.Count > _capacity)
            {
                int oldTick = _order.Dequeue();
                _byTick.Remove(oldTick);
            }
        }

        public void Clear()
        {
            _byTick.Clear();
            _order.Clear();
        }

        public bool TryGetInterpolationPair(double renderTick, out SnapshotMsg a, out SnapshotMsg b, out double alpha)
        {
            a = null!;
            b = null!;
            alpha = 0;

            if (_order.Count < 2)
                return false;

            // Find nearest ticks around renderTick
            // Simple linear search (OK for small buffers). Upgrade to sorted list if needed.
            int bestA = int.MinValue;
            int bestB = int.MaxValue;

            foreach (int t in _order)
            {
                if (t <= renderTick && t > bestA)
                    bestA = t;
                if (t >= renderTick && t < bestB)
                    bestB = t;
            }

            if (bestA == int.MinValue || bestB == int.MaxValue)
                return false;

            if (!_byTick.TryGetValue(bestA, out SnapshotMsg? snapA) || snapA == null)
                return false;
            if (!_byTick.TryGetValue(bestB, out SnapshotMsg? snapB) || snapB == null)
                return false;
            a = snapA;
            b = snapB;

            if (bestA == bestB)
            {
                alpha = 0;
                return true;
            }

            alpha = (renderTick - bestA) / (bestB - bestA);
            if (alpha < 0) alpha = 0;
            if (alpha > 1) alpha = 1;

            return true;
        }

        // Compatibility methods for old ClientSim code
        public bool TryGetPairForTickDouble(double renderTick, out SnapshotMsg a, out SnapshotMsg b, out double t)
        {
            return TryGetInterpolationPair(renderTick, out a, out b, out t);
        }

        public bool TryGet(int tick, out SnapshotMsg snapshot)
        {
            return _byTick.TryGetValue(tick, out snapshot!);
        }
    }
}
