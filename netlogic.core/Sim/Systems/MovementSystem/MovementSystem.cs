// FILE: netlogic.core/Sim/Systems/MovementSystem.cs
// MovementSystem auto-discovers handlers in the same assembly.
using Game;

namespace Sim.Systems
{
    public sealed class MovementSystem : EngineCommandSinkBase<EngineCommandType>
    {
        public MovementSystem() : base(handlers: DiscoverHandlersForSystem(systemType: typeof(MovementSystem)))
        {
        }
    }
}
