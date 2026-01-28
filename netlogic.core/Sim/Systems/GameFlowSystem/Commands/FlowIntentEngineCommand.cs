using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.systems.gameflowsystem.commands
{
    /// <summary>
    /// Player intent command for game flow.
    /// Payload is generic (Param0) to avoid a command type explosion.
    /// Determinism rule: this is purely player-authored intent.
    /// </summary>
    public sealed class FlowIntentEngineCommand : EngineCommand<EngineCommandType>
    {
        public GameFlowIntent Intent { get; }
        public int Param0 { get; }

        // Only the latest flow intent per tick/connection wins by default.
        public override int ReplaceKey => 0;

        public FlowIntentEngineCommand(GameFlowIntent intent, int param0 = 0)
            : base(EngineCommandType.FlowFire)
        {
            Intent = intent;
            Param0 = param0;
        }

        public static FlowIntentEngineCommand ClickNewGame() => new FlowIntentEngineCommand(GameFlowIntent.ClickNewGame);

        public static FlowIntentEngineCommand SelectChefHat(int hatId) =>
            new FlowIntentEngineCommand(GameFlowIntent.SelectChefHat, hatId);

        public static FlowIntentEngineCommand ClickStartRun() => new FlowIntentEngineCommand(GameFlowIntent.ClickStartRun);

        public static FlowIntentEngineCommand ClickServeCustomer(int customerIndex) =>
            new FlowIntentEngineCommand(GameFlowIntent.ClickServeCustomer, customerIndex);

        public static FlowIntentEngineCommand ClickCook() => new FlowIntentEngineCommand(GameFlowIntent.ClickCook);

        public static FlowIntentEngineCommand ClickContinue() => new FlowIntentEngineCommand(GameFlowIntent.ClickContinue);

        public static FlowIntentEngineCommand ClickConcedeRun() => new FlowIntentEngineCommand(GameFlowIntent.ClickConcedeRun);

        public static FlowIntentEngineCommand ReturnToMenu() => new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu);
    }
}
