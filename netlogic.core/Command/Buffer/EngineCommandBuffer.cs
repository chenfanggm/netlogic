namespace Sim
{
    /// <summary>
    /// Buffers engine commands by tick and connection, validates scheduling bounds,
    /// and dequeues sorted command batches for execution.
    /// </summary>
    public sealed class EngineCommandBuffer(
        int maxFutureTicks = 2,
        int maxPastTicks = 2,
        int maxStoredTicks = 4)
    {
        private readonly Dictionary<int, Dictionary<int, EngineCommandBucket>> _byTickThenConn = [];
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

            EngineCommandBucket bucket = GetOrCreateBucket(scheduledTick, connId);
            bucket.MergeReplace(clientCmdSeq, commands);
            return true;
        }

        public IEnumerable<int> ConnectionIdsForTick(int tick)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, EngineCommandBucket>? byConn) || byConn == null)
                yield break;

            foreach (int connId in byConn.Keys)
                yield return connId;
        }

        public bool TryDequeueForTick(int tick, int connectionId, out EngineCommandBatch batch)
        {
            batch = default;

            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, EngineCommandBucket>? byConn) || byConn == null)
                return false;

            if (!byConn.TryGetValue(connectionId, out EngineCommandBucket? bucket) || bucket == null)
                return false;

            List<EngineCommand> list = bucket.MaterializeSorted();
            batch = new EngineCommandBatch(tick, bucket.MaxClientCmdSeq, list);
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

        private EngineCommandBucket GetOrCreateBucket(int tick, int connId)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, EngineCommandBucket>? byConn) || byConn == null)
            {
                byConn = new Dictionary<int, EngineCommandBucket>();
                _byTickThenConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connId, out EngineCommandBucket? bucket) || bucket == null)
            {
                bucket = new EngineCommandBucket();
                byConn.Add(connId, bucket);
            }

            return bucket;
        }

    }
}
