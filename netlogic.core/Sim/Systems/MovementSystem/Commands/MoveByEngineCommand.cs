using Game;

namespace Sim
{
    public sealed class MoveByEngineCommand : EngineCommand<EngineCommandType>
    {
        public int EntityId { get; }
        public int Dx { get; }
        public int Dy { get; }

        public override int ReplaceKey => EntityId;

        public MoveByEngineCommand(int entityId, int dx, int dy)
            : base(EngineCommandType.MoveBy)
        {
            EntityId = entityId;
            Dx = dx;
            Dy = dy;
        }
    }
}
