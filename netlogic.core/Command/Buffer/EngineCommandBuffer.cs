using System;
using System.Collections.Generic;

namespace Sim
{
    /// <summary>
    /// Buffers authoritative EngineCommands by (scheduledTick, connId).
    ///
    /// Responsibilities:
    /// 1) Scheduling policy:
    ///    - Reject commands that are too old: clientTick < (serverTick - MaxPastTicks)
    ///    - Clamp commands too far in the future to (serverTick + MaxFutureTicks)
    ///    - Snap slightly-late commands to "now" (serverTick) since we don't rollback.
    ///
    /// 2) Per-tick replacement:
    ///    - Within the same (tick, connId) bucket, commands with the same semantic key
    ///      (e.g. (Type, ReplaceKey)) replace each other ("latest intent wins").
    ///      This prevents duplicates from retries/jitter from executing twice.
    ///
    /// 3) Dequeue:
    ///    - Emits a stable, sorted batch for deterministic execution.
    ///
    /// Note: This class is NOT a world-state system. It normalizes input before it reaches systems.
    /// </summary>
    public sealed class EngineCommandBuffer(
        int maxFutureTicks = 2,
        int maxPastTicks = 2,
        int maxStoredTicks = 4)
    {
        // scheduledTick -> connId -> bucket
        private readonly Dictionary<int, Dictionary<int, EngineCommandBucket>> _byTickThenConn = [];

        private readonly int _maxFutureTicks = maxFutureTicks;
        private readonly int _maxPastTicks = maxPastTicks;
        private readonly int _maxStoredTicks = maxStoredTicks;

        /// <summary>
        /// Attempts to enqueue a set of commands coming from a client.
        ///
        /// Returns:
        /// - true if accepted (possibly clamped/snapped)
        /// - false if rejected (too old) or empty input
        /// </summary>
        public bool EnqueueWithValidation(
            int connId,
            int clientRequestedTick,
            uint clientCmdSeq,
            List<EngineCommand> commands,
            int currentServerTick)
        {
            if (commands == null || commands.Count == 0)
                return false;

            // Reject if too old (outside the allowed past window).
            int minAcceptedTick = currentServerTick - _maxPastTicks;
            if (clientRequestedTick < minAcceptedTick)
                return false;

            int maxAcceptedTick = currentServerTick + _maxFutureTicks;

            // Compute where the server will actually schedule these commands.
            int scheduledTick = ComputeScheduledTick(clientRequestedTick, currentServerTick, maxAcceptedTick);

            EngineCommandBucket bucket = GetOrCreateBucket(scheduledTick, connId);

            // MergeReplace enforces "latest intent wins" replacement inside the bucket.
            bucket.MergeReplace(clientCmdSeq, commands);

            return true;
        }

        /// <summary>
        /// Returns all connIds that currently have buffered commands for the given tick.
        /// </summary>
        public IEnumerable<int> GetConnIdsByTick(int tick)
        {
            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, EngineCommandBucket>? byConn) || byConn == null)
                yield break;

            foreach (int connId in byConn.Keys)
                yield return connId;
        }

        /// <summary>
        /// Dequeues the buffered batch for (tick, connId).
        /// The returned batch commands are stable-sorted for deterministic execution.
        /// </summary>
        public bool TryDequeueForTick(int tick, int connId, out EngineCommandBatch batch)
        {
            batch = default;

            if (!_byTickThenConn.TryGetValue(tick, out Dictionary<int, EngineCommandBucket>? byConn) || byConn == null)
                return false;

            if (!byConn.TryGetValue(connId, out EngineCommandBucket? bucket) || bucket == null)
                return false;

            List<EngineCommand> commands = bucket.MaterializeSorted();
            batch = new EngineCommandBatch(tick, bucket.MaxClientCmdSeq, commands);

            // Remove consumed bucket.
            byConn.Remove(connId);
            if (byConn.Count == 0)
                _byTickThenConn.Remove(tick);

            return true;
        }

        /// <summary>
        /// Drops ticks older than (currentTick - _maxStoredTicks).
        /// Call once per server tick to bound memory usage.
        /// </summary>
        public void DropOldTick(int currentTick)
        {
            int oldestAllowedTick = currentTick - _maxStoredTicks;
            if (_byTickThenConn.Count == 0)
                return;

            List<int> toRemove = new List<int>(16);

            foreach (int tick in _byTickThenConn.Keys)
            {
                if (tick < oldestAllowedTick)
                    toRemove.Add(tick);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _byTickThenConn.Remove(toRemove[i]);
        }

        private static int ComputeScheduledTick(int requestedClientTick, int currentServerTick, int maxAcceptedTick)
        {
            // Too far in the future => clamp.
            if (requestedClientTick > maxAcceptedTick)
                return maxAcceptedTick;

            // Slightly late (but still within MaxPastTicks) => snap to now.
            // We do not rollback/resim, so "execute in the past" is not supported.
            if (requestedClientTick < currentServerTick)
                return currentServerTick;

            // In range => keep requested tick.
            return requestedClientTick;
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
