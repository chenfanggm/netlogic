// FILE: netlogic.core/Sim/Systems/MovementSystem/Handlers/MoveByHandler.cs
using Game;

namespace Sim.Systems
{
    [EngineCommandHandler(typeof(MovementSystem))]
    internal sealed class MoveByHandler : EngineCommandHandlerBase<MoveByEngineCommand, EngineCommandType>
    {
        public MoveByHandler() : base(EngineCommandType.MoveBy)
        {
        }

        public override void Handle(World world, MoveByEngineCommand command)
        {
            world.TryMoveEntityBy(command.EntityId, command.Dx, command.Dy);
        }
    }
}
