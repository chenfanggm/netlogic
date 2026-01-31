using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.movementsystem.commands
{
    public sealed class DashEngineCommand : EngineCommand<EngineCommandType>
    {
        public int EntityId { get; }
        public int Dx { get; }
        public int Dy { get; }

        public override int ReplaceKey => EntityId;

        public DashEngineCommand(int entityId, int dx, int dy)
            : base(EngineCommandType.Dash)
        {
            EntityId = entityId;
            Dx = dx;
            Dy = dy;
        }
    }
}
