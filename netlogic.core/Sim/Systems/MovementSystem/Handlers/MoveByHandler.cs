// FILE: netlogic.core/Sim/Systems/MovementSystem/Handlers/MoveByHandler.cs
using Sim.Game;
using Sim.System;

namespace Sim.Command
{
    [EngineCommandHandler(typeof(MovementSystem))]
    internal sealed class MoveByHandler : EngineCommandHandlerBase<MoveByEngineCommand, EngineCommandType>
    {
        public MoveByHandler() : base(EngineCommandType.MoveBy)
        {
        }

        public override void Handle(Game.TheGame world, MoveByEngineCommand command)
        {
            world.TryMoveEntityBy(command.EntityId, command.Dx, command.Dy);
        }
    }
}
