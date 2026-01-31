using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.movementsystem.commands
{
    public sealed class GrantHasteEngineCommand : EngineCommand<EngineCommandType>
    {
        public int EntityId { get; }
        public int DurationTicks { get; }

        public override int ReplaceKey => EntityId;

        public GrantHasteEngineCommand(int entityId, int durationTicks)
            : base(EngineCommandType.GrantHaste)
        {
            EntityId = entityId;
            DurationTicks = durationTicks;
        }
    }
}
