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
        private readonly Queue<int> _tickOrder;

        public SnapshotRingBuffer(int capacity)
        {
            if (capacity < 4)
                throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be >= 4");

            _capacity = capacity;
            _byTick = new Dictionary<int, SnapshotMsg>(capacity);
            _tickOrder = new Queue<int>(capacity);
        }

        public int Count
        {
            get { return _byTick.Count; }
        }

        public void Add(SnapshotMsg snapshot)
        {
            int tick = snapshot.Tick;

            if (_byTick.ContainsKey(tick))
            {
                // Replace if already exists (rare, but ok)
                _byTick[tick] = snapshot;
                return;
            }

            if (_byTick.Count >= _capacity)
            {
                int oldestTick = _tickOrder.Dequeue();
                _byTick.Remove(oldestTick);
            }

            _byTick.Add(tick, snapshot);
            _tickOrder.Enqueue(tick);
        }

        public bool TryGet(int tick, out SnapshotMsg snapshot)
        {
            return _byTick.TryGetValue(tick, out snapshot!);
        }

        public bool TryGetPairForTickDouble(double renderTick, out SnapshotMsg a, out SnapshotMsg b, out double t)
        {
            a = null!;
            b = null!;
            t = 0.0;

            if (_byTick.Count < 2)
                return false;

            int tickA = (int)Math.Floor(renderTick);
            int tickB = tickA + 1;


            if (!_byTick.TryGetValue(tickA, out SnapshotMsg? snapA) || snapA == null)
                return false;
            if (!_byTick.TryGetValue(tickB, out SnapshotMsg? snapB) || snapB == null)
                return false;

            double frac = renderTick - tickA;
            if (frac < 0.0) frac = 0.0;
            if (frac > 1.0) frac = 1.0;

            a = snapA;
            b = snapB;
            t = frac;
            return true;
        }
    }
}
