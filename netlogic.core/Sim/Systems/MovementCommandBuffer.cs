using System.Collections.Generic;

namespace Sim.Systems
{
    public sealed class MovementCommandBuffer
    {
        // tick -> connId -> list of movement commands
        private readonly Dictionary<int, Dictionary<int, List<ClientCommand>>> _buf =
            new Dictionary<int, Dictionary<int, List<ClientCommand>>>();

        public void Enqueue(int tick, int connId, in ClientCommand cmd)
        {
            if (!_buf.TryGetValue(tick, out Dictionary<int, List<ClientCommand>>? byConn) || byConn == null)
            {
                byConn = new Dictionary<int, List<ClientCommand>>();
                _buf.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connId, out List<ClientCommand>? list) || list == null)
            {
                list = new List<ClientCommand>(8);
                byConn.Add(connId, list);
            }

            list.Add(cmd);
        }

        public bool TryGetForTick(int tick, out Dictionary<int, List<ClientCommand>>? byConn)
        {
            return _buf.TryGetValue(tick, out byConn);
        }

        public void ClearTick(int tick)
        {
            _buf.Remove(tick);
        }

        public void DropBeforeTick(int oldestAllowedTick)
        {
            if (_buf.Count == 0)
                return;

            List<int> toRemove = new List<int>(16);
            bool hasRemovals = false;

            foreach (int t in _buf.Keys)
            {
                if (t < oldestAllowedTick)
                {
                    toRemove.Add(t);
                    hasRemovals = true;
                }
            }

            if (!hasRemovals)
                return;

            for (int i = 0; i < toRemove.Count; i++)
                _buf.Remove(toRemove[i]);
        }
    }
}
