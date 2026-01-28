using System;
using System.Collections.Generic;
using com.aqua.netlogic.command;

namespace com.aqua.netlogic.command.buffer
{
    /// <summary>
    /// Single-thread command buffer.
    ///
    /// Ownership rule:
    /// - This buffer MUST only be accessed from the server/engine thread.
    /// - Do not call Enqueue/TryDequeue/GetConnIdsByTick from multiple threads.
    ///
    /// If you ever need multi-thread enqueue, put a thread-safe queue at the boundary
    /// (transport thread -> server thread), then call EnqueueWithValidation on the server thread.
    /// </summary>
    internal sealed class EngineCommandBuffer<TCommandType>
        where TCommandType : struct, Enum
    {
        private readonly int _maxPastTicks;
        private readonly int _maxFutureTicks;
        private readonly int _lateGraceTicks;
        private readonly int _maxStoredTicks;

        // tick -> connId -> bucket
        private readonly Dictionary<int, Dictionary<int, EngineCommandBucket<TCommandType>>> _byTick =
            new Dictionary<int, Dictionary<int, EngineCommandBucket<TCommandType>>>(256);

        public long DroppedTooOldCount { get; private set; }
        public long SnappedLateCount { get; private set; }
        public long ClampedFutureCount { get; private set; }
        public long AcceptedCount { get; private set; }

        public EngineCommandBuffer(int maxPastTicks, int maxFutureTicks, int lateGraceTicks, int maxStoredTicks)
        {
            _maxPastTicks = Math.Max(0, maxPastTicks);
            _maxFutureTicks = Math.Max(0, maxFutureTicks);
            _lateGraceTicks = Math.Max(0, lateGraceTicks);
            _maxStoredTicks = Math.Max(1, maxStoredTicks);
        }

        public bool EnqueueWithValidation(
            int currentServerTick,
            int connId,
            int clientRequestedTick,
            uint clientCmdSeq,
            List<EngineCommand<TCommandType>> commands)
        {
            if (commands == null || commands.Count == 0)
                return false;

            int minAcceptedTick = currentServerTick - _maxPastTicks;
            int tooOldDropTick = minAcceptedTick - _lateGraceTicks;

            if (clientRequestedTick < tooOldDropTick)
            {
                DroppedTooOldCount++;
                return false;
            }

            int maxAcceptedTick = currentServerTick + _maxFutureTicks;

            int scheduledTick;
            if (clientRequestedTick < minAcceptedTick)
            {
                scheduledTick = currentServerTick;
                SnappedLateCount++;
            }
            else if (clientRequestedTick > maxAcceptedTick)
            {
                scheduledTick = maxAcceptedTick;
                ClampedFutureCount++;
            }
            else
            {
                scheduledTick = clientRequestedTick;
            }

            if (!_byTick.TryGetValue(scheduledTick, out Dictionary<int, EngineCommandBucket<TCommandType>>? byConn))
            {
                byConn = new Dictionary<int, EngineCommandBucket<TCommandType>>(32);
                _byTick.Add(scheduledTick, byConn);
            }

            if (!byConn.TryGetValue(connId, out EngineCommandBucket<TCommandType>? bucket))
            {
                bucket = new EngineCommandBucket<TCommandType>();
                byConn.Add(connId, bucket);
            }

            bucket.MergeReplace(clientCmdSeq, commands);

            TrimOldTicks_NoAlloc(currentServerTick);
            AcceptedCount++;
            return true;
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

        private void TrimOldTicks_NoAlloc(int currentServerTick)
        {
            // Keep a small window; prevent unbounded growth under jitter/attack.
            // Conservative: allow storage around current tick.
            int minKeep = currentServerTick - _maxStoredTicks;

            if (_byTick.Count == 0)
                return;

            List<int>? toRemove = null;
            foreach (int t in _byTick.Keys)
            {
                if (t < minKeep)
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
