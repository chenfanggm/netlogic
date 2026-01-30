// FILE: netlogic.core/Sim/Systems/MovementSystem/Handlers/MoveByHandler.cs
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.systems.movementsystem.commands;
using com.aqua.netlogic.command.handler;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.movementsystem.handlers
{
    [EngineCommandHandler(typeof(MovementSystem))]
    internal sealed class MoveByHandler : EngineCommandHandlerBase<MoveByEngineCommand, EngineCommandType>
    {
        public MoveByHandler() : base(EngineCommandType.MoveBy)
        {
        }

        public override void Handle(com.aqua.netlogic.sim.game.ServerModel world, MoveByEngineCommand command)
        {
            world.TryMoveEntityBy(command.EntityId, command.Dx, command.Dy);
        }
    }
}
