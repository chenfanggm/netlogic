namespace Sim
{
    public readonly struct EngineCommandBatch(int scheduledTick, uint clientCmdSeq, List<EngineCommand> commands)
    {
        public readonly int ScheduledTick = scheduledTick;
        public readonly uint ClientCmdSeq = clientCmdSeq;
        public readonly List<EngineCommand> Commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }
}
