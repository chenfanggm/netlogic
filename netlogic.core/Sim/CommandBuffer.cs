using System.Collections.Generic;
using Net;

namespace Sim
{
    public sealed class CommandBuffer
    {
        // tick -> commands
        private readonly Dictionary<int, List<Command>> _byTick = new();

        public void Add(Command cmd)
        {
            if (!_byTick.TryGetValue(cmd.TargetTick, out List<Command>? list) || list == null)
            {
                list = new List<Command>(8);
                _byTick[cmd.TargetTick] = list;
            }
            list.Add(cmd);
        }

        public List<Command> Drain(int tick)
        {
            if (_byTick.TryGetValue(tick, out List<Command>? list) && list != null)
            {
                _byTick.Remove(tick);
                return list;
            }
            return s_empty;
        }

        private static readonly List<Command> s_empty = new(0);
    }
}
