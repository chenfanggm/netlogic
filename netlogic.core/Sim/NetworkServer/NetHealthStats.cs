namespace Sim.Server
{
    public readonly struct NetHealthStats
    {
        public readonly long DroppedTooOld;
        public readonly long SnappedLate;
        public readonly long ClampedFuture;
        public readonly long Accepted;

        public NetHealthStats(long droppedTooOld, long snappedLate, long clampedFuture, long accepted)
        {
            DroppedTooOld = droppedTooOld;
            SnappedLate = snappedLate;
            ClampedFuture = clampedFuture;
            Accepted = accepted;
        }

        public override string ToString()
        {
            return $"cmd.accept={Accepted} cmd.drop_old={DroppedTooOld} cmd.snap_late={SnappedLate} cmd.clamp_future={ClampedFuture}";
        }
    }
}
