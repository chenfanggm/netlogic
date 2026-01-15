// FILE: netlogic.core/Sim/Systems/MovementSystem.cs
// Option-2 (B): flat tick-local inbox, cleared every Execute().
// No multi-tick storage. DropOldTick is no-op.

using System.Collections.Generic;
using Game;
using Sim.Commanding;

namespace Sim.Systems
{
    public sealed class MovementSystem : ISystemCommandSink
    {
        public string Name => "Movement";

        public IReadOnlyList<ClientCommandType> OwnedCommandTypes => _owned;
        private static readonly ClientCommandType[] _owned =
        {
            ClientCommandType.MoveBy,
        };

        // Tick-local inbox (Option-2).
        private readonly List<ClientCommand> _inbox = new List<ClientCommand>(256);

        public void EnqueueCommand(int tick, int connId, in ClientCommand command)
        {
            // Called only during CommandSystem.DispatchTick(tick) inside TickOnce.
            _inbox.Add(command);
        }

        public void Execute(int tick, ref World world)
        {
            for (int i = 0; i < _inbox.Count; i++)
            {
                ClientCommand c = _inbox[i];
                world.TryMoveEntityBy(c.EntityId, c.Dx, c.Dy);
            }

            _inbox.Clear();
        }
    }
}
