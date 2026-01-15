using Game;

namespace Sim.Systems
{
    public interface IGameSystem
    {
        /// <summary>Stable system order is enforced by ServerEngine (array order).</summary>
        string Name { get; }

        /// <summary>
        /// Execute one fixed tick worth of logic.
        /// Systems may consume their own routed commands for this tick.
        /// </summary>
        void Execute(int tick, ref World world, SystemInputs inputs);
    }
}
