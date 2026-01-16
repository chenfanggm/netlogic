using System.Collections.Generic;

namespace Sim.Commanding
{
    /// <summary>
    /// A gameplay system that can receive routed engine commands for a tick.
    /// The system owns the queue/buffer of those commands and consumes them during Execute().
    /// </summary>
    public interface ISystemCommandSink
    {
        string Name { get; }

        /// <summary>
        /// Declare which engine command types this system owns.
        /// CommandSystem will auto-register routes based on this list.
        /// </summary>
        IReadOnlyList<EngineCommandType> CommandTypes { get; }

        /// <summary>Called by CommandSystem during routing.</summary>
        void EnqueueCommand(int tick, int connId, EngineCommand command);

        /// <summary>Called by ServerEngine in stable order.</summary>
        void Execute(int tick, Game.World world);
    }
}
