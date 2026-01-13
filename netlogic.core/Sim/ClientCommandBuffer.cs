using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    /// <summary>
    /// Stores client commands by scheduled tick.
    /// Server consumes commands in TickOnce() for current tick.
    /// </summary>
    public sealed class ClientCommandBuffer
    {
        private readonly Dictionary<int, Dictionary<int, Queue<ClientOpsMsg>>> _byTickByConn;

        public ClientCommandBuffer()
        {
            _byTickByConn = new Dictionary<int, Dictionary<int, Queue<ClientOpsMsg>>>(256);
        }

        public void Enqueue(int connId, ClientOpsMsg msg)
        {
            int tick = msg.ClientTick;

            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ClientOpsMsg>>? byConn))
            {
                byConn = new Dictionary<int, Queue<ClientOpsMsg>>(32);
                _byTickByConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connId, out Queue<ClientOpsMsg>? q))
            {
                q = new Queue<ClientOpsMsg>(16);
                byConn.Add(connId, q);
            }

            q.Enqueue(msg);
        }

        public bool TryDequeueForTick(int tick, int connId, out ClientOpsMsg msg)
        {
            msg = null!;
            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ClientOpsMsg>>? byConn))
                return false;

            if (!byConn.TryGetValue(connId, out Queue<ClientOpsMsg>? q))
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
            // Prevent unbounded growth if clients send ancient commands
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
