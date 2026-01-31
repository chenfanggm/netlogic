using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.entity;
using com.aqua.netlogic.sim.game.rules;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.systems.movementsystem.commands;
using com.aqua.netlogic.command.handler;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.movementsystem.handlers
{
    [EngineCommandHandler(typeof(MovementSystem))]
    internal sealed class DashHandler : EngineCommandHandlerBase<DashEngineCommand, EngineCommandType>
    {
        private const int DashDistance = 2;
        private const int DashCooldownTicks = 5;

        public DashHandler() : base(EngineCommandType.Dash)
        {
        }

        public override void Handle(ServerModel world, OpWriter ops, DashEngineCommand command)
        {
            if (!world.TryGet(command.EntityId, out Entity e))
                return;

            if (e.DashCooldownTicksRemaining > 0)
                return;

            int dx = command.Dx;
            int dy = command.Dy;

            if (dx != 0)
            {
                dx = dx > 0 ? 1 : -1;
                dy = 0;
            }
            else if (dy != 0)
            {
                dy = dy > 0 ? 1 : -1;
                dx = 0;
            }
            else
            {
                return;
            }

            int dist = DashDistance;
            if (e.HasteTicksRemaining > 0)
                dist += 1;

            int newX = e.X + dx * dist;
            int newY = e.Y + dy * dist;

            ops.EmitAndApply(RepOp.PositionSnapshot(command.EntityId, newX, newY));
            ops.EmitAndApply(RepOp.EntityCooldownSet(command.EntityId, CooldownType.Dash, DashCooldownTicks));
        }
    }
}
