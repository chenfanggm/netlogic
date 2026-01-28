namespace com.aqua.netlogic.sim.networkserver
{
    public readonly struct CommandIngressStats
    {
        public readonly long DroppedTooOld;
        public readonly long SnappedLate;
        public readonly long ClampedFuture;

        public CommandIngressStats(long droppedTooOld, long snappedLate, long clampedFuture)
        {
            DroppedTooOld = droppedTooOld;
            SnappedLate = snappedLate;
            ClampedFuture = clampedFuture;
        }
    }
}
