namespace Sim
{
    public enum EngineCommandType : byte
    {
        None = 0,
        MoveBy = 1
    }

    /// <summary>
    /// Base class for all commands that can be enqueued into <see cref="ServerEngine"/>.
    /// Engine commands are authoritative: the server can accept, schedule, validate, and route them.
    /// </summary>
    public abstract class EngineCommand
    {
        public EngineCommandType Type { get; }

        protected EngineCommand(EngineCommandType type)
        {
            Type = type;
        }
    }

    public sealed class MoveByEngineCommand : EngineCommand
    {
        public int EntityId { get; }
        public int Dx { get; }
        public int Dy { get; }

        public MoveByEngineCommand(int entityId, int dx, int dy)
            : base(EngineCommandType.MoveBy)
        {
            EntityId = entityId;
            Dx = dx;
            Dy = dy;
        }
    }
}
