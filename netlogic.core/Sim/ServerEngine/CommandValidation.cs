namespace com.aqua.netlogic.sim.serverengine
{
    public static class CommandValidation
    {
        public const int MaxFutureTicks = 2;
        public const int MaxPastTicks = 2;

        public const bool ShiftLateToCurrentTick = true;
        public const bool ClampFutureToMax = true;
    }
}
