using System;
using System.Collections.Generic;

namespace Sim
{
    /// <summary>
    /// Stores decoded ClientCommand lists by scheduled tick + connection.
    /// This avoids re-encoding to ops on server side.
    /// </summary>
    public sealed class ClientCommandBuffer2
    {
        private readonly Dictionary<int, Dictionary<int, Queue<ScheduledCommandBatch>>> _byTickByConn;

        public ClientCommandBuffer2()
        {
            _byTickByConn = new Dictionary<int, Dictionary<int, Queue<ScheduledCommandBatch>>>(256);
        }

        public bool EnqueueWithValidation(int connectionId, int requestedClientTick, uint clientCmdSeq, List<ClientCommand> commands, int currentServerTick, out int scheduledTick)
        {
            scheduledTick = requestedClientTick;

            // Too old: drop
            if (scheduledTick < currentServerTick - CommandValidationConfig.MaxPastTicks)
            {
                return false;
            }

            // Late but acceptable: shift to current tick
            if (scheduledTick < currentServerTick)
            {
                if (CommandValidationConfig.ShiftLateToCurrentTick)
                {
                    scheduledTick = currentServerTick;
                }
                else
                {
                    return false;
                }
            }

            // Too far future: clamp or drop
            int maxAllowedFuture = currentServerTick + CommandValidationConfig.MaxFutureTicks;
            if (scheduledTick > maxAllowedFuture)
            {
                if (CommandValidationConfig.ClampFutureToMax)
                {
                    scheduledTick = maxAllowedFuture;
                }
                else
                {
                    return false;
                }
            }

            EnqueueInternal(connectionId, scheduledTick, clientCmdSeq, commands);

            return true;
        }

        private void EnqueueInternal(int connectionId, int tick, uint clientCmdSeq, List<ClientCommand> commands)
        {
            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledCommandBatch>>? byConn))
            {
                byConn = new Dictionary<int, Queue<ScheduledCommandBatch>>(32);
                _byTickByConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connectionId, out Queue<ScheduledCommandBatch>? q))
            {
                q = new Queue<ScheduledCommandBatch>(16);
                byConn.Add(connectionId, q);
            }

            ClientCommand[] copy = CopyCommands(commands);

            ScheduledCommandBatch batch = new ScheduledCommandBatch(tick, clientCmdSeq, copy);
            q.Enqueue(batch);
        }

        public IEnumerable<int> ConnectionIdsForTick(int tick)
        {
            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledCommandBatch>>? byConn))
                yield break;

            foreach (KeyValuePair<int, Queue<ScheduledCommandBatch>> kv in byConn)
                yield return kv.Key;
        }

        public bool TryDequeueForTick(int tick, int connectionId, out ScheduledCommandBatch batch)
        {
            batch = default(ScheduledCommandBatch);

            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledCommandBatch>>? byConn))
                return false;

            if (!byConn.TryGetValue(connectionId, out Queue<ScheduledCommandBatch>? q))
                return false;

            if (q.Count == 0)
                return false;

            batch = q.Dequeue();
            return true;
        }

        public void DropBeforeTick(int tickExclusive)
        {
            if (_byTickByConn.Count == 0)
                return;

            List<int> toRemove = new List<int>();

            foreach (KeyValuePair<int, Dictionary<int, Queue<ScheduledCommandBatch>>> kv in _byTickByConn)
            {
                if (kv.Key < tickExclusive)
                    toRemove.Add(kv.Key);
            }

            int i = 0;
            while (i < toRemove.Count)
            {
                _byTickByConn.Remove(toRemove[i]);
                i++;
            }
        }

        private static ClientCommand[] CopyCommands(List<ClientCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return Array.Empty<ClientCommand>();

            ClientCommand[] dst = new ClientCommand[commands.Count];

            int i = 0;
            while (i < commands.Count)
            {
                dst[i] = commands[i];
                i++;
            }

            return dst;
        }

        public readonly struct ScheduledCommandBatch
        {
            public readonly int Tick;
            public readonly uint ClientCmdSeq;
            public readonly ClientCommand[] Commands;

            public ScheduledCommandBatch(int tick, uint clientCmdSeq, ClientCommand[] commands)
            {
                Tick = tick;
                ClientCmdSeq = clientCmdSeq;
                Commands = commands;
            }
        }
    }
}
