using System;

namespace com.aqua.netlogic.net.time
{
    /// <summary>
    /// Estimates server time/tick on the client using Ping/Pong:
    /// - Ping carries client send time (ms)
    /// - Pong echoes client send time, and includes serverTimeMs + serverTick at send time.
    ///
    /// We estimate:
    /// - RTT (ms)
    /// - serverTimeOffsetMs such that: serverTime ~= clientNow + offset
    /// </summary>
    public sealed class TimeSyncEstimator
    {
        // Smoothed values
        public double RttMs { get; private set; }
        public double ServerTimeOffsetMs { get; private set; } // serverTime ~= clientNow + offset
        public int LastServerTick { get; private set; }
        public double LastServerTimeMs { get; private set; }

        // Config
        private const double RttAlpha = 0.15;     // smoothing factor
        private const double OffsetAlpha = 0.15;

        public bool HasLock { get; private set; }

        public void SeedFromWelcome(double serverTimeMs, int serverTick)
        {
            LastServerTimeMs = serverTimeMs;
            LastServerTick = serverTick;

            // At seed time we don't know offset; caller should call UpdateOnPong soon.
            HasLock = false;
        }

        /// <summary>
        /// Call when Pong arrives.
        ///
        /// clientSendMs = time when Ping was sent (echoed back)
        /// clientRecvMs = time when Pong was received
        /// serverTimeMs = server time when Pong was sent
        /// serverTick = server tick when Pong was sent
        /// </summary>
        public void UpdateOnPong(long clientSendMs, long clientRecvMs, double serverTimeMs, int serverTick)
        {
            double rtt = Math.Max(0, clientRecvMs - clientSendMs);
            RttMs = HasLock ? Lerp(RttMs, rtt, RttAlpha) : rtt;

            // NTP-style: server time at client receive ~= serverTimeMs + rtt/2
            double serverAtClientRecv = serverTimeMs + (rtt * 0.5);
            double offset = serverAtClientRecv - clientRecvMs;

            ServerTimeOffsetMs = HasLock ? Lerp(ServerTimeOffsetMs, offset, OffsetAlpha) : offset;

            LastServerTimeMs = serverTimeMs;
            LastServerTick = serverTick;

            HasLock = true;
        }

        public double EstimateServerNowMs(long clientNowMs)
        {
            return clientNowMs + ServerTimeOffsetMs;
        }

        public int EstimateServerTick(long clientNowMs, int serverTickRateHz)
        {
            if (serverTickRateHz <= 0)
                return LastServerTick;

            // If we have lock, project tick from server-time.
            // tick ~= LastServerTick + (serverNow - LastServerTimeMs) * tickRateHz / 1000
            double serverNow = EstimateServerNowMs(clientNowMs);
            double dtMs = serverNow - LastServerTimeMs;
            int dtTicks = (int)Math.Round(dtMs * serverTickRateHz / 1000.0);

            return LastServerTick + dtTicks;
        }

        public int PlanCommandTargetTick(long clientNowMs, int serverTickRateHz, int inputLeadTicks)
        {
            int est = EstimateServerTick(clientNowMs, serverTickRateHz);
            return est + Math.Max(0, inputLeadTicks);
        }

        private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
    }
}
