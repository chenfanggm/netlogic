using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.rules;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.systems.movementsystem.commands;
using com.aqua.netlogic.command.handler;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.movementsystem.handlers
{
    [EngineCommandHandler(typeof(MovementSystem))]
    internal sealed class GrantHasteHandler : EngineCommandHandlerBase<GrantHasteEngineCommand, EngineCommandType>
    {
        public GrantHasteHandler() : base(EngineCommandType.GrantHaste)
        {
        }

        public override void Handle(ServerModel world, OpWriter ops, GrantHasteEngineCommand command)
        {
            if (!world.TryGet(command.EntityId, out _))
                return;

            ops.EmitAndApply(RepOp.EntityBuffSet(command.EntityId, BuffType.Haste, command.DurationTicks));
        }
    }
}
