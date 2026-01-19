// FILE: netlogic.core/Sim/Systems/MovementSystem.cs
// MovementSystem auto-discovers handlers in the same assembly.

namespace Sim.Systems
{
    public sealed class MovementSystem : EngineCommandSinkBase
    {
        public MovementSystem() : base(handlers: DiscoverHandlersForSystem(systemType: typeof(MovementSystem)))
        {
        }
    }
}
