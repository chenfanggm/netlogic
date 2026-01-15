using Game;

namespace Sim.Commands
{
    public sealed class MoveByCommandHandler : IClientCommandHandler
    {
        public ClientCommandType Type => ClientCommandType.MoveBy;

        public void Apply(World world, in ClientCommand command)
        {
            world.TryMoveEntityBy(command.EntityId, command.Dx, command.Dy);
        }
    }
}
