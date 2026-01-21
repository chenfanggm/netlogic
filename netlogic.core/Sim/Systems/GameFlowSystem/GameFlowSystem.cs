// FILE: netlogic.core/Sim/Systems/GameFlowSystem/GameFlowSystem.cs
// GameFlowSystem auto-discovers handlers in the same assembly.

namespace Sim.Systems
{
    public sealed class GameFlowSystem : EngineCommandSinkBase<EngineCommandType>
    {
        public GameFlowSystem() : base(handlers: DiscoverHandlersForSystem(systemType: typeof(GameFlowSystem)))
        {
        }
    }
}
