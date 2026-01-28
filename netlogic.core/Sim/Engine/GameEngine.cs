using Sim.Game;

namespace Sim.Engine
{
    /// <summary>
    /// TEMP SHIM: kept to avoid breaking existing code while migrating to ServerEngine.
    /// Remove once all call sites are updated.
    /// </summary>
    [global::System.Obsolete("Use ServerEngine instead. This shim will be removed after migration.")]
    public sealed class GameEngine : ServerEngine
    {
        public GameEngine(Game.Game initialGame) : base(initialGame) { }
    }
}
