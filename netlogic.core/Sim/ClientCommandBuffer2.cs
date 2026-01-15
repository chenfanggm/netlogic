using System;
using System.Collections.Generic;

namespace Sim
{
    public sealed class ClientCommandBuffer2
    {
        public readonly struct CommandBatch
        {
            public readonly int ScheduledTick;
            public readonly uint ClientCmdSeq;
            public readonly List<ClientCommand> Commands;

            public CommandBatch(int scheduledTick, uint clientCmdSeq, List<ClientCommand> commands)
            {
                ScheduledTick = scheduledTick;
                ClientCmdSeq = clientCmdSeq;
                Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            }
        }

        private readonly Dictionary<int, Dictionary<int, Queue<CommandBatch>>> _byTickThenConn =
            new Dictionary<int, Dictionary<int, Queue<CommandBatch>>>();

        // Optional: dedup per connection (future). For now: keep simple.
        // private readonly Dictionary<int, uint> _lastSeqByConn = new();

        public int MaxFutureTicks { get; set; } = 2;
        public int MaxPastTicks { get; set; } = 2;

        /// <summary>
        /// Enqueues a batch with validation/scheduling.
        /// Returns false if dropped; true if enqueued. Outputs scheduledTick.
        /// </summary>
        public bool EnqueueWithValidation(
            int connectionId,
            int clientTick,
            uint clientCmdSeq,
            List<ClientCommand> commands,
            int serverTick)
        {
            int scheduledTick = clientTick;
            if (commands == null || commands.Count == 0)
            {
                scheduledTick = serverTick;
                return false;
            }

            // Normalize requested tick into server scheduling window.
            int minTick = serverTick - MaxPastTicks;
            int maxTick = serverTick + MaxFutureTicks;

            if (clientTick < minTick)
            {
                // Too old => drop
                scheduledTick = clientTick;
                return false;
            }

            if (clientTick > maxTick)
            {
                // Too far future => clamp to max allowed (or drop if you want)
                scheduledTick = maxTick;
            }
            else if (clientTick < serverTick)
            {
                // Late => shift to current tick
                scheduledTick = serverTick;
            }
            else
            {
                scheduledTick = clientTick;
            }

            EnqueueInternal(scheduledTick, connectionId, new CommandBatch(scheduledTick, clientCmdSeq, commands));
            return true;
        }

        public IEnumerable<int> ConnectionIdsForTick(int tick)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<CommandBatch>>? byConn) || byConn == null)
                yield break;

            foreach (int connId in byConn.Keys)
                yield return connId;
        }

        public bool TryDequeueForTick(int tick, int connectionId, out CommandBatch batch)
        {
            batch = default;

            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<CommandBatch>>? byConn) || byConn == null)
                return false;

            if (!byConn.TryGetValue(connectionId, out Queue<CommandBatch>? q) || q == null)
                return false;

            if (q.Count == 0)
                return false;

            batch = q.Dequeue();

            if (q.Count == 0)
                byConn.Remove(connectionId);

            if (byConn.Count == 0)
                _byTickThenConn.Remove(tick);

            return true;
        }

        public void DropBeforeTick(int oldestAllowedTick)
        {
            if (_byTickThenConn.Count == 0)
                return;

            // Collect keys to remove (avoid modifying during enumeration)
            List<int> toRemove = new List<int>(16);
            bool hasRemovals = false;

            foreach (int tick in _byTickThenConn.Keys)
            {
                if (tick < oldestAllowedTick)
                {
                    toRemove.Add(tick);
                    hasRemovals = true;
                }
            }

            if (!hasRemovals)
                return;

            int i = 0;
            while (i < toRemove.Count)
            {
                _byTickThenConn.Remove(toRemove[i]);
                i++;
            }
        }

        private void EnqueueInternal(int tick, int connId, CommandBatch batch)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<CommandBatch>>? byConn) || byConn == null)
            {
                byConn = new Dictionary<int, Queue<CommandBatch>>();
                _byTickThenConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connId, out Queue<CommandBatch>? q) || q == null)
            {
                q = new Queue<CommandBatch>();
                byConn.Add(connId, q);
            }

            q.Enqueue(batch);
        }
    }
}
