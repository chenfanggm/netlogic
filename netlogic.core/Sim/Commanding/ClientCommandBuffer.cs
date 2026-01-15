using System;
using System.Collections.Generic;

namespace Sim
{
    public sealed class ClientCommandBuffer
    {
        private readonly Dictionary<int, Dictionary<int, Queue<CommandBatch>>> _byTickThenConn =
            new Dictionary<int, Dictionary<int, Queue<CommandBatch>>>();

        // Optional: dedup per connection (future). For now: keep simple.
        // private readonly Dictionary<int, uint> _lastSeqByConn = new();

        public int MaxFutureTicks { get; }
        public int MaxPastTicks { get; }

        public ClientCommandBuffer(int maxFutureTicks = 2, int maxPastTicks = 2)
        {
            MaxFutureTicks = maxFutureTicks;
            MaxPastTicks = maxPastTicks;
        }

        /// <summary>
        /// Enqueues a batch with validation/scheduling.
        /// Returns false if dropped; true if enqueued. Outputs scheduledTick.
        /// </summary>
        public bool EnqueueWithValidation(
            int connectionId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<ClientCommand> commands,
            int currentServerTick)
        {
            int scheduledTick = requestedClientTick;
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

        public void DropOldTick(int oldestAllowedTick)
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
