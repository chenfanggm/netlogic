namespace Sim
{
    /// <summary>
    /// RTT estimator using exponential weighted moving average (EWMA).
    /// </summary>
    public sealed class RttEstimator
    {
        private double _smoothedRttMs;
        private bool _has;

        public double SmoothedRttMs => _smoothedRttMs;

        public void AddSample(double rttMs)
        {
            if (!_has)
            {
                _smoothedRttMs = rttMs;
                _has = true;
                return;
            }

            // EWMA
            double alpha = 0.15;
            _smoothedRttMs = (_smoothedRttMs * (1.0 - alpha)) + (rttMs * alpha);
        }
    }
}
