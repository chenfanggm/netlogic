using System;
using System.Collections.Generic;

namespace Sim.Command
{
    /// <summary>
    /// Stores replace-merged commands for a single tick/connection and materializes
    /// them in a deterministic sort order.
    /// </summary>
    internal sealed class EngineCommandBucket<TCommandType>
        where TCommandType : struct, Enum
    {
        private readonly Dictionary<(TCommandType Type, int ReplaceKey), EngineCommand<TCommandType>> _map =
            new Dictionary<(TCommandType Type, int ReplaceKey), EngineCommand<TCommandType>>(64);

        public uint MaxClientCmdSeq { get; private set; }

        public void MergeReplace(uint clientCmdSeq, List<EngineCommand<TCommandType>> commands)
        {
            if (clientCmdSeq > MaxClientCmdSeq)
                MaxClientCmdSeq = clientCmdSeq;

            for (int i = 0; i < commands.Count; i++)
            {
                EngineCommand<TCommandType> cmd = commands[i];
                if (cmd == null)
                    continue;

                _map[(cmd.Type, cmd.ReplaceKey)] = cmd;
            }
        }

        public List<EngineCommand<TCommandType>> MaterializeSorted()
        {
            if (_map.Count == 0)
                return new List<EngineCommand<TCommandType>>(0);

            // Sort keys deterministically without allocating a KeyValuePair list.
            var keys = new (TCommandType Type, int ReplaceKey)[_map.Count];
            int n = 0;
            foreach (var kv in _map)
                keys[n++] = kv.Key;

            Array.Sort(keys, (a, b) =>
            {
                int typeCompare = Comparer<TCommandType>.Default.Compare(a.Type, b.Type);
                if (typeCompare != 0)
                    return typeCompare;
                return a.ReplaceKey.CompareTo(b.ReplaceKey);
            });

            var list = new List<EngineCommand<TCommandType>>(keys.Length);
            for (int i = 0; i < keys.Length; i++)
                list.Add(_map[keys[i]]);

            return list;
        }
    }
}
