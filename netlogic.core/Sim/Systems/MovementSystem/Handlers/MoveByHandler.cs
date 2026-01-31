// FILE: netlogic.core/Sim/Systems/MovementSystem/Handlers/MoveByHandler.cs
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.entity;
using com.aqua.netlogic.sim.systems.movementsystem.commands;
using com.aqua.netlogic.command.handler;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.movementsystem.handlers
{
    [EngineCommandHandler(typeof(MovementSystem))]
    internal sealed class MoveByHandler : EngineCommandHandlerBase<MoveByEngineCommand, EngineCommandType>
    {
        public MoveByHandler() : base(EngineCommandType.MoveBy)
        {
        }

        public override void Handle(com.aqua.netlogic.sim.game.ServerModel world, OpWriter ops, MoveByEngineCommand command)
        {
            if (!world.TryGet(command.EntityId, out Entity entity))
                return;

            int dx = command.Dx;
            int dy = command.Dy;

            if (entity.HasteTicksRemaining > 0)
            {
                if (dx != 0)
                    dx += dx > 0 ? 1 : -1;
                else if (dy != 0)
                    dy += dy > 0 ? 1 : -1;
            }

            int newX = entity.X + dx;
            int newY = entity.Y + dy;
            ops.EmitAndApply(RepOp.PositionSnapshot(command.EntityId, newX, newY));
        }
    }
}
