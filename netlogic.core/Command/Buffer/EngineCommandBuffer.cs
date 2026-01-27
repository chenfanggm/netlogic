using System;
using System.Collections.Generic;

namespace Sim.Command
{
    /// <summary>
    /// Single-thread command buffer.
    ///
    /// Ownership rule:
    /// - This buffer MUST only be accessed from the server/engine thread.
    /// - Do not call Enqueue/TryDequeue/GetConnIdsByTick from multiple threads.
    ///
    /// If you ever need multi-thread enqueue, put a thread-safe queue at the boundary
    /// (transport thread -> server thread), then call EnqueueCommands on the server thread.
    /// </summary>
    internal sealed class EngineCommandBuffer<TCommandType>
        where TCommandType : struct, Enum
    {
        private readonly int _maxPastTicks;
        private readonly int _maxFutureTicks;

        // tick -> connId -> bucket
        private readonly Dictionary<int, Dictionary<int, EngineCommandBucket<TCommandType>>> _byTick =
            new Dictionary<int, Dictionary<int, EngineCommandBucket<TCommandType>>>(256);

        public EngineCommandBuffer(int maxPastTicks, int maxFutureTicks)
        {
            _maxPastTicks = Math.Max(0, maxPastTicks);
            _maxFutureTicks = Math.Max(0, maxFutureTicks);
        }

        public void EnqueueCommands(int currentServerTick, int connId, int clientTick, uint clientCmdSeq, List<EngineCommand<TCommandType>> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            // Too old? Drop.
            int oldestAllowed = currentServerTick - _maxPastTicks;
            if (clientTick < oldestAllowed)
                return;

            // Too far future? Clamp to avoid unbounded storage.
            int newestAllowed = currentServerTick + _maxFutureTicks;
            int effectiveTick = clientTick;
            if (clientTick > newestAllowed)
                effectiveTick = newestAllowed;
            else if (clientTick < currentServerTick)
                effectiveTick = currentServerTick;

            if (!_byTick.TryGetValue(effectiveTick, out Dictionary<int, EngineCommandBucket<TCommandType>>? byConn))
            {
                byConn = new Dictionary<int, EngineCommandBucket<TCommandType>>(32);
                _byTick.Add(effectiveTick, byConn);
            }

            if (!byConn.TryGetValue(connId, out EngineCommandBucket<TCommandType>? bucket))
            {
                bucket = new EngineCommandBucket<TCommandType>();
                byConn.Add(connId, bucket);
            }

            bucket.MergeReplace(clientCmdSeq, commands);

            // Optional: simple memory hygiene. Since we keep past ticks for _maxPastTicks,
            // we can prune anything older than oldestAllowed - 1.
            PruneOldTicks(oldestAllowed);
        }

        public bool TryDequeueForTick(int tick, int connId, out EngineCommandBatch<TCommandType> batch)
        {
            batch = default;

            if (!_byTick.TryGetValue(tick, out Dictionary<int, EngineCommandBucket<TCommandType>>? byConn))
                return false;

            if (!byConn.TryGetValue(connId, out EngineCommandBucket<TCommandType>? bucket) || bucket == null)
                return false;

            // Materialize deterministically.
            List<EngineCommand<TCommandType>> cmds = bucket.MaterializeSorted();
            batch = new EngineCommandBatch<TCommandType>(tick, bucket.MaxClientCmdSeq, cmds);

            // Remove consumed bucket.
            byConn.Remove(connId);
            if (byConn.Count == 0)
                _byTick.Remove(tick);

            return true;
        }

        public IEnumerable<int> GetConnIdsByTick(int tick)
        {
            if (!_byTick.TryGetValue(tick, out Dictionary<int, EngineCommandBucket<TCommandType>>? byConn) || byConn == null || byConn.Count == 0)
                return Array.Empty<int>();

            // Return a copy: callers must not observe internal dictionary keys.
            int[] ids = new int[byConn.Count];
            int i = 0;
            foreach (int connId in byConn.Keys)
                ids[i++] = connId;

            return ids;
        }

        private void PruneOldTicks(int oldestAllowedTick)
        {
            // Remove anything strictly older than oldestAllowedTick.
            // NOTE: dictionary iteration while removing requires collecting keys first.
            if (_byTick.Count == 0)
                return;

            List<int>? toRemove = null;
            foreach (int t in _byTick.Keys)
            {
                if (t < oldestAllowedTick)
                {
                    toRemove ??= new List<int>(8);
                    toRemove.Add(t);
                }
            }

            if (toRemove == null)
                return;

            for (int i = 0; i < toRemove.Count; i++)
                _byTick.Remove(toRemove[i]);
        }
    }
}
