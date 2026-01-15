using Game;

namespace Sim.Commands
{
    public interface IClientCommandHandler
    {
        ClientCommandType Type { get; }

        /// <summary>
        /// Apply one command to the world (deterministic).
        /// </summary>
        void Apply(World world, in ClientCommand command);
    }
}
