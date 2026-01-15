using Game;

namespace Sim.Systems
{
    public sealed class MovementSystem : IGameSystem
    {
        public string Name => "Movement";

        public void Execute(int tick, ref World world, SystemInputs inputs)
        {
            if (!inputs.Router.Movement.TryGetForTick(tick, out Dictionary<int, List<ClientCommand>>? byConn) || byConn == null)
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

            inputs.Router.Movement.ClearTick(tick);
        }
    }
}
