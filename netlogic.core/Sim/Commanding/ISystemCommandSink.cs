namespace Sim.Commanding
{
    /// <summary>
    /// A gameplay system that can receive routed client commands for a tick.
    /// The system owns the queue/buffer of those commands and consumes them during Execute().
    /// </summary>
    public interface ISystemCommandSink
    {
        string Name { get; }

        /// <summary>Called by CommandSystem when routing commands.</summary>
        void EnqueueCommand(int tick, int connId, in ClientCommand command);

        /// <summary>
        /// Called by ServerEngine in stable order. System should consume commands for the tick.
        /// </summary>
        void Execute(int tick, ref Game.World world);
    }
}
