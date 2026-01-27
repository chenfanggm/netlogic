using System;

namespace Sim.Server
{
    /// <summary>
    /// Rolling 60-second network health metrics (server side).
    /// Call Tick(nowMs) once per server tick, and RecordSend(...) on each send.
    /// </summary>
    internal sealed class ServerNetMetrics
    {
        private const int WindowSeconds = 60;

        private readonly long[] _reliableBytesPerSec = new long[WindowSeconds];
        private readonly long[] _unreliableBytesPerSec = new long[WindowSeconds];
        private readonly int[] _baselinesPerSec = new int[WindowSeconds];

        private int _secCursor;
        private long _lastSecKey; // floor(nowMs/1000)

        // lifetime counters
        public long TotalReliableBytes { get; private set; }
        public long TotalUnreliableBytes { get; private set; }
        public long TotalBaselinesSent { get; private set; }

        public void Tick(double nowMs)
        {
            long secKey = (long)Math.Floor(nowMs / 1000.0);
            if (_lastSecKey == 0)
            {
                _lastSecKey = secKey;
                return;
            }

            while (_lastSecKey < secKey)
            {
                _lastSecKey++;
                _secCursor = (_secCursor + 1) % WindowSeconds;

                _reliableBytesPerSec[_secCursor] = 0;
                _unreliableBytesPerSec[_secCursor] = 0;
                _baselinesPerSec[_secCursor] = 0;
            }
        }

        public void RecordReliableBytes(int bytes)
        {
            if (bytes <= 0)
                return;
            _reliableBytesPerSec[_secCursor] += bytes;
            TotalReliableBytes += bytes;
        }

        public void RecordUnreliableBytes(int bytes)
        {
            if (bytes <= 0)
                return;
            _unreliableBytesPerSec[_secCursor] += bytes;
            TotalUnreliableBytes += bytes;
        }

        public void RecordBaselineSent()
        {
            _baselinesPerSec[_secCursor] += 1;
            TotalBaselinesSent += 1;
        }

        public Snapshot GetSnapshot()
        {
            long relSum = 0;
            long unrelSum = 0;
            int baseSum = 0;
            int baseMin = int.MaxValue;
            int baseMax = 0;

            for (int i = 0; i < WindowSeconds; i++)
            {
                relSum += _reliableBytesPerSec[i];
                unrelSum += _unreliableBytesPerSec[i];

                int b = _baselinesPerSec[i];
                baseSum += b;
                if (b < baseMin) baseMin = b;
                if (b > baseMax) baseMax = b;
            }

            if (baseMin == int.MaxValue)
                baseMin = 0;

            // “per second” rates averaged over the window
            double relBps = relSum / (double)WindowSeconds;
            double unrelBps = unrelSum / (double)WindowSeconds;

            return new Snapshot(
                avgReliableBytesPerSec: relBps,
                avgUnreliableBytesPerSec: unrelBps,
                baselinesTotalLast60s: baseSum,
                baselinesMinPerSec: baseMin,
                baselinesMaxPerSec: baseMax);
        }

        internal readonly struct Snapshot
        {
            public readonly double AvgReliableBytesPerSec;
            public readonly double AvgUnreliableBytesPerSec;
            public readonly int BaselinesTotalLast60s;
            public readonly int BaselinesMinPerSec;
            public readonly int BaselinesMaxPerSec;

            public Snapshot(
                double avgReliableBytesPerSec,
                double avgUnreliableBytesPerSec,
                int baselinesTotalLast60s,
                int baselinesMinPerSec,
                int baselinesMaxPerSec)
            {
                AvgReliableBytesPerSec = avgReliableBytesPerSec;
                AvgUnreliableBytesPerSec = avgUnreliableBytesPerSec;
                BaselinesTotalLast60s = baselinesTotalLast60s;
                BaselinesMinPerSec = baselinesMinPerSec;
                BaselinesMaxPerSec = baselinesMaxPerSec;
            }
        }
    }
}
