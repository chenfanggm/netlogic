namespace Sim
{
    public sealed class EngineCommandBuffer(
        int maxFutureTicks = 2,
        int maxPastTicks = 2,
        int maxStoredTicks = 4)
    {
        private readonly Dictionary<int, Dictionary<int, Queue<EngineCommandBatch>>> _byTickThenConn =
            new Dictionary<int, Dictionary<int, Queue<EngineCommandBatch>>>();
        private readonly int _maxStoredTicks = maxStoredTicks;

        public int MaxFutureTicks { get; } = maxFutureTicks;
        public int MaxPastTicks { get; } = maxPastTicks;

        public bool EnqueueWithValidation(
            int connId,
            int clientTick,
            uint clientCmdSeq,
            List<EngineCommand> commands,
            int serverTick)
        {
            int scheduledTick = clientTick;
            if (commands == null || commands.Count == 0)
            {
                scheduledTick = serverTick;
                return false;
            }

            int minTick = serverTick - MaxPastTicks;
            int maxTick = serverTick + MaxFutureTicks;

            if (clientTick < minTick)
            {
                scheduledTick = clientTick;
                return false;
            }

            if (clientTick > maxTick)
            {
                scheduledTick = maxTick;
            }
            else if (clientTick < serverTick)
            {
                scheduledTick = serverTick;
            }
            else
            {
                scheduledTick = clientTick;
            }

            EnqueueInternal(scheduledTick, connId, new EngineCommandBatch(scheduledTick, clientCmdSeq, commands));
            return true;
        }

        public IEnumerable<int> ConnectionIdsForTick(int tick)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<EngineCommandBatch>>? byConn) || byConn == null)
                yield break;

            foreach (int connId in byConn.Keys)
                yield return connId;
        }

        public bool TryDequeueForTick(int tick, int connectionId, out EngineCommandBatch batch)
        {
            batch = default;

            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<EngineCommandBatch>>? byConn) || byConn == null)
                return false;

            if (!byConn.TryGetValue(connectionId, out Queue<EngineCommandBatch>? q) || q == null)
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

        public void DropOldTick(int currentTick)
        {
            int oldestAllowedTick = currentTick - _maxStoredTicks;

            if (_byTickThenConn.Count == 0)
                return;

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

        private void EnqueueInternal(int tick, int connId, EngineCommandBatch batch)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, Queue<EngineCommandBatch>>? byConn) || byConn == null)
            {
                byConn = new Dictionary<int, Queue<EngineCommandBatch>>();
                _byTickThenConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connId, out Queue<EngineCommandBatch>? q) || q == null)
            {
                q = new Queue<EngineCommandBatch>();
                byConn.Add(connId, q);
            }

            q.Enqueue(batch);
        }
    }
}
