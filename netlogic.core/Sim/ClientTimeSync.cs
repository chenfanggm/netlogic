using System;
using System.Diagnostics;

namespace Sim
{
    /// <summary>
    /// Client-side time synchronization that estimates server tick using local clock and received snapshots.
    /// </summary>
    public sealed class ClientTimeSync(int tickRateHz)
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        private int _lastServerTick = 0;
        private long _lastServerTickReceivedAtMs = 0;

        private int _tickRateHz = tickRateHz;

        public void SetTickRate(int tickRateHz)
        {
            _tickRateHz = tickRateHz;
        }

        public void OnSnapshotReceived(int serverTick)
        {
            _lastServerTick = serverTick;
            _lastServerTickReceivedAtMs = _stopwatch.ElapsedMilliseconds;
        }

        public double GetEstimatedServerTickDouble()
        {
            long nowMs = _stopwatch.ElapsedMilliseconds;
            long elapsedMs = nowMs - _lastServerTickReceivedAtMs;

            double ticksPerMs = (double)_tickRateHz / 1000.0;
            double tickOffset = elapsedMs * ticksPerMs;

            double estimated = (double)_lastServerTick + tickOffset;
            return estimated;
        }

        public int GetEstimatedServerTickFloor()
        {
            double est = GetEstimatedServerTickDouble();
            int floor = (int)Math.Floor(est);
            return floor;
        }
    }
}
