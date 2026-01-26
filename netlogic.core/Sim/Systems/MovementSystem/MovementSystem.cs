// FILE: netlogic.core/Sim/Systems/MovementSystem.cs
// MovementSystem auto-discovers handlers in the same assembly.
using Sim.Game;

namespace Sim.Command
{
    public sealed class MovementSystem : CommandSinkBase<EngineCommandType>
    {
        public MovementSystem() : base(handlers: DiscoverHandlersForSystem(systemType: typeof(MovementSystem)))
        {
        }
    }
}
