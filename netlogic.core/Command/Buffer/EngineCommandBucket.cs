namespace Sim
{
    /// <summary>
    /// Stores replace-merged commands for a single tick/connection and materializes
    /// them in a deterministic sort order.
    /// </summary>
    internal sealed class EngineCommandBucket
    {
        private readonly Dictionary<long, EngineCommand> _map = new Dictionary<long, EngineCommand>(64);

        public uint MaxClientCmdSeq { get; private set; }

        public void MergeReplace(uint clientCmdSeq, List<EngineCommand> commands)
        {
            if (clientCmdSeq > MaxClientCmdSeq)
                MaxClientCmdSeq = clientCmdSeq;

            for (int i = 0; i < commands.Count; i++)
            {
                EngineCommand cmd = commands[i];
                if (cmd == null)
                    continue;

                long key = MakeKey(cmd.Type, cmd.ReplaceKey);
                _map[key] = cmd;
            }
        }

        public List<EngineCommand> MaterializeSorted()
        {
            List<KeyValuePair<long, EngineCommand>> items = [.. _map];

            items.Sort((a, b) => a.Key.CompareTo(b.Key));

            List<EngineCommand> list = new List<EngineCommand>(items.Count);
            for (int i = 0; i < items.Count; i++)
                list.Add(items[i].Value);

            return list;
        }

        private static long MakeKey(EngineCommandType type, int replaceKey)
        {
            long t = (long)(byte)type;
            long rk = (uint)replaceKey;
            return (t << 32) | rk;
        }
    }
}
