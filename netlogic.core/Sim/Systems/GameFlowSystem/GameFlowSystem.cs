// FILE: netlogic.core/Sim/Systems/GameFlowSystem/GameFlowSystem.cs
// GameFlowSystem auto-discovers handlers in the same assembly.

using Sim.Game;

namespace Sim.Command
{
    public sealed class GameFlowSystem : CommandSinkBase<EngineCommandType>
    {
        public GameFlowSystem() : base(handlers: DiscoverHandlersForSystem(systemType: typeof(GameFlowSystem)))
        {
        }
    }
}
