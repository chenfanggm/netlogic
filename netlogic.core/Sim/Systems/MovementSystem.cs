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

        public IReadOnlyList<EngineCommandType> CommandTypes => _owned;
        private static readonly EngineCommandType[] _owned =
        {
            EngineCommandType.MoveBy,
        };

        // Tick-local inbox (Option-2).
        private readonly List<MoveByEngineCommand> _inbox = new List<MoveByEngineCommand>(256);

        public void EnqueueCommand(int tick, int connId, EngineCommand command)
        {
            // Called only during CommandSystem.Dispatch(tick) inside TickOnce.
            if (command is MoveByEngineCommand move)
                _inbox.Add(move);
        }

        public void Execute(int tick, World world)
        {
            for (int i = 0; i < _inbox.Count; i++)
            {
                MoveByEngineCommand c = _inbox[i];
                world.TryMoveEntityBy(c.EntityId, c.Dx, c.Dy);
            }

            _inbox.Clear();
        }
    }
}
