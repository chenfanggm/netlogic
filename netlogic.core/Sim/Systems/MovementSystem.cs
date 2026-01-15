using System.Collections.Generic;
using Game;
using Sim.Commanding;

namespace Sim.Systems
{
    public sealed class MovementSystem : ISystemCommandSink
    {
        public string Name => "Movement";

        private readonly Dictionary<int, Dictionary<int, List<ClientCommand>>> _buf =
            new Dictionary<int, Dictionary<int, List<ClientCommand>>>();

        public void EnqueueCommand(int tick, int connId, in ClientCommand command)
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

            list.Add(command);
        }

        public void Execute(int tick, ref World world)
        {
            if (!_buf.TryGetValue(tick, out Dictionary<int, List<ClientCommand>>? byConn) || byConn == null)
                return;

            int[] connIds = DeterministicKeys.GetSortedKeys(byConn);

            for (int i = 0; i < connIds.Length; i++)
            {
                int connId = connIds[i];
                List<ClientCommand> list = byConn[connId];

                for (int j = 0; j < list.Count; j++)
                {
                    ClientCommand c = list[j];
                    world.TryMoveEntityBy(c.EntityId, c.Dx, c.Dy);
                }
            }

            _buf.Remove(tick);
        }
    }
}
