using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    /// <summary>
    /// Stores client commands by scheduled tick.
    /// Server consumes commands in TickOnce() for current tick.
    /// Also supports simple scheduling normalization:
    /// - late commands (within MaxPastTicks) can be shifted to current tick
    /// - far-future commands can be clamped to current + MaxFutureTicks
    /// - too-old commands are dropped
    /// </summary>
    public sealed class ClientCommandBuffer
    {
        private readonly Dictionary<int, Dictionary<int, Queue<ClientOpsMsg>>> _byTickByConn;

        public ClientCommandBuffer()
        {
            _byTickByConn = new Dictionary<int, Dictionary<int, Queue<ClientOpsMsg>>>(256);
        }

        public bool EnqueueWithValidation(int connectionId, ClientOpsMsg msg, int currentServerTick, out int scheduledTick)
        {
            scheduledTick = msg.ClientTick;

            // Too old: drop
            if (scheduledTick < currentServerTick - CommandValidationConfig.MaxPastTicks)
            {
                return false;
            }

            // Late but acceptable: shift to current tick
            if (scheduledTick < currentServerTick)
            {
                if (CommandValidationConfig.ShiftLateToCurrentTick)
                {
                    scheduledTick = currentServerTick;
                }
                else
                {
                    return false;
                }
            }

            // Too far in the future: clamp or drop
            int maxAllowedFuture = currentServerTick + CommandValidationConfig.MaxFutureTicks;
            if (scheduledTick > maxAllowedFuture)
            {
                if (CommandValidationConfig.ClampFutureToMax)
                {
                    scheduledTick = maxAllowedFuture;
                }
                else
                {
                    return false;
                }
            }

            ClientOpsMsg normalized = new ClientOpsMsg(
                clientTick: scheduledTick,
                clientCmdSeq: msg.ClientCmdSeq,
                opCount: msg.OpCount,
                opsPayload: msg.OpsPayload);

            EnqueueInternal(connectionId, normalized);

            return true;
        }

        private void EnqueueInternal(int connectionId, ClientOpsMsg msg)
        {
            int tick = msg.ClientTick;

            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ClientOpsMsg>>? byConn))
            {
                byConn = new Dictionary<int, Queue<ClientOpsMsg>>(32);
                _byTickByConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connectionId, out Queue<ClientOpsMsg>? q))
            {
                q = new Queue<ClientOpsMsg>(16);
                byConn.Add(connectionId, q);
            }

            q.Enqueue(msg);
        }

        public bool TryDequeueForTick(int tick, int connectionId, out ClientOpsMsg msg)
        {
            msg = null!;
            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ClientOpsMsg>>? byConn))
                return false;

            if (!byConn.TryGetValue(connectionId, out Queue<ClientOpsMsg>? q))
                return false;

            if (q.Count == 0)
                return false;

            msg = q.Dequeue();
            return true;
        }

        public IEnumerable<int> ConnectionIdsForTick(int tick)
        {
            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ClientOpsMsg>>? byConn))
                yield break;

            foreach (KeyValuePair<int, Queue<ClientOpsMsg>> kv in byConn)
                yield return kv.Key;
        }

        public void DropBeforeTick(int tickExclusive)
        {
            if (_byTickByConn.Count == 0)
                return;

            List<int> toRemove = new List<int>();

            foreach (KeyValuePair<int, Dictionary<int, Queue<ClientOpsMsg>>> kv in _byTickByConn)
            {
                int t = kv.Key;
                if (t < tickExclusive)
                    toRemove.Add(t);
            }

            int i = 0;
            while (i < toRemove.Count)
            {
                _byTickByConn.Remove(toRemove[i]);
                i++;
            }
        }
    }
}
