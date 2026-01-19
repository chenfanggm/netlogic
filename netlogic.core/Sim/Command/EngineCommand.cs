namespace Sim
{
    /// <summary>
    /// Base class for all commands that can be enqueued into <see cref="ServerEngine"/>.
    /// Engine commands are authoritative: the server can accept, schedule, validate, and route them.
    /// </summary>
    public abstract class EngineCommand(EngineCommandType type)
    {
        public EngineCommandType Type { get; } = type;
    }
}
