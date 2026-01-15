using System.Collections.Generic;

namespace Sim.Systems
{
    /// <summary>
    /// Routes commands into per-system tick buffers.
    /// - Adapter/engine enqueues batches by scheduled tick.
    /// - During TickOnce, systems consume routed commands for that tick.
    ///
    /// No pooling, no reuse: simplest correct implementation.
    /// </summary>
    public sealed class CommandRouter
    {
        // Per-system buffers
        private readonly MovementCommandBuffer _movement = new MovementCommandBuffer();

        public MovementCommandBuffer Movement => _movement;

        /// <summary>
        /// Route a whole batch into per-system buffers for the given scheduled tick.
        /// </summary>
        public void RouteBatch(int scheduledTick, int connId, List<ClientCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                ClientCommand c = commands[i];

                // Route by command type -> owning system buffer.
                // This is the only "switch" you need, and it stays small because it's per-system ownership.
                switch (c.Type)
                {
                    case ClientCommandType.MoveBy:
                        _movement.Enqueue(scheduledTick, connId, c);
                        break;

                    default:
                        // Unknown command: ignore for now (or log in debug)
                        break;
                }
            }
        }

        /// <summary>
        /// Clears all system buffers older than oldestAllowedTick.
        /// </summary>
        public void DropBeforeTick(int oldestAllowedTick)
        {
            _movement.DropBeforeTick(oldestAllowedTick);
        }

        /// <summary>
        /// Optional: clear buffers for an already-executed tick (keeps memory down).
        /// </summary>
        public void ClearTick(int tick)
        {
            _movement.ClearTick(tick);
        }
    }
}
