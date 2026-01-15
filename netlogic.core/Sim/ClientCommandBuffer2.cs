using System;
using System.Collections.Generic;

namespace Sim
{
    public sealed class ClientCommandBuffer2
    {
        public readonly struct ScheduledBatch
        {
            public readonly int ScheduledTick;
            public readonly uint ClientCmdSeq;
            public readonly List<ClientCommand> Commands;

            public ScheduledBatch(int scheduledTick, uint clientCmdSeq, List<ClientCommand> commands)
            {
                ScheduledTick = scheduledTick;
                ClientCmdSeq = clientCmdSeq;
                Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            }
        }

        private readonly Dictionary<int, Dictionary<int, Queue<ScheduledBatch>>> _byTickThenConn =
            new Dictionary<int, Dictionary<int, Queue<ScheduledBatch>>>();

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
            int requestedClientTick,
            uint clientCmdSeq,
            List<ClientCommand> commands,
            int currentServerTick,
            out int scheduledTick)
        {
            if (commands == null || commands.Count == 0)
            {
                scheduledTick = currentServerTick;
                return false;
            }

            // Normalize requested tick into server scheduling window.
            int minTick = currentServerTick - MaxPastTicks;
            int maxTick = currentServerTick + MaxFutureTicks;

            if (requestedClientTick < minTick)
            {
                // Too old => drop
                scheduledTick = requestedClientTick;
                return false;
            }

            if (requestedClientTick > maxTick)
            {
                // Too far future => clamp to max allowed (or drop if you want)
                scheduledTick = maxTick;
            }
            else if (requestedClientTick < currentServerTick)
            {
                // Late => shift to current tick
                scheduledTick = currentServerTick;
            }
            else
            {
                scheduledTick = requestedClientTick;
            }

            EnqueueInternal(scheduledTick, connectionId, new ScheduledBatch(scheduledTick, clientCmdSeq, commands));
            return true;
        }

        public IEnumerable<int> ConnectionIdsForTick(int tick)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledBatch>>? byConn) || byConn == null)
                yield break;

            foreach (int connId in byConn.Keys)
                yield return connId;
        }

        public bool TryDequeueForTick(int tick, int connectionId, out ScheduledBatch batch)
        {
            batch = default;

            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledBatch>>? byConn) || byConn == null)
                return false;

            if (!byConn.TryGetValue(connectionId, out Queue<ScheduledBatch>? q) || q == null)
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

        private void EnqueueInternal(int tick, int connId, ScheduledBatch batch)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledBatch>>? byConn) || byConn == null)
            {
                byConn = new Dictionary<int, Queue<ScheduledBatch>>();
                _byTickThenConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connId, out Queue<ScheduledBatch>? q) || q == null)
            {
                q = new Queue<ScheduledBatch>();
                byConn.Add(connId, q);
            }

            q.Enqueue(batch);
        }
    }
}
