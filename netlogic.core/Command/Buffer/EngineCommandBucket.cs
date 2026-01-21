using System;
using System.Collections.Generic;

namespace Sim
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
            List<KeyValuePair<(TCommandType Type, int ReplaceKey), EngineCommand<TCommandType>>> items = [.. _map];

            items.Sort((a, b) =>
            {
                int typeCompare = Comparer<TCommandType>.Default.Compare(a.Key.Type, b.Key.Type);
                if (typeCompare != 0)
                    return typeCompare;
                return a.Key.ReplaceKey.CompareTo(b.Key.ReplaceKey);
            });

            List<EngineCommand<TCommandType>> list = new List<EngineCommand<TCommandType>>(items.Count);
            for (int i = 0; i < items.Count; i++)
                list.Add(items[i].Value);

            return list;
        }
    }
}
