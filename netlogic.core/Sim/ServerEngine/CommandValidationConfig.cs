using System;

namespace Sim.Engine
{
    public static class CommandValidationConfig
    {
        // Accept a little client clock skew/jitter.
        public const int MaxFutureTicks = 2;

        // Allow small lateness; beyond this we treat it as too old.
        public const int MaxPastTicks = 2;

        // If a command is late but within MaxPastTicks, we shift it to current tick.
        public const bool ShiftLateToCurrentTick = true;

        // If a command is too far in the future, clamp to (current + MaxFutureTicks).
        public const bool ClampFutureToMax = true;
    }
}
