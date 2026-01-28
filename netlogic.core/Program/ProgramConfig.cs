namespace com.aqua.netlogic.program
{
    public readonly struct ProgramConfig(int tickRateHz, TimeSpan? maxRunDuration = null)
    {
        public int TickRateHz { get; init; } = tickRateHz;
        public TimeSpan? MaxRunDuration { get; init; } = maxRunDuration;
    }
}
