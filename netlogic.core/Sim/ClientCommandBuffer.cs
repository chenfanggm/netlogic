using System;
using System.Collections.Generic;

namespace Sim
{
    /// <summary>
    /// Buffers decoded ClientCommand batches by tick and connection.
    /// ServerEngine consumes only in TickOnce() for deterministic sim.
    /// </summary>
    public sealed class ClientCommandBuffer
    {
        private readonly Dictionary<int, Dictionary<int, Queue<ScheduledBatch>>> _byTickByConn;

        public ClientCommandBuffer()
        {
            _byTickByConn = new Dictionary<int, Dictionary<int, Queue<ScheduledBatch>>>(256);
        }

        public bool EnqueueWithValidation(
            int connectionId,
            int requestedClientTick,
            uint clientCmdSeq,
            ClientCommand[] commands,
            int currentServerTick,
            out int scheduledTick)
        {
            scheduledTick = requestedClientTick;

            if (commands == null || commands.Length == 0)
                return false;

            // Too old -> drop
            if (scheduledTick < currentServerTick - CommandValidation.MaxPastTicks)
                return false;

            // Late but acceptable -> shift to current
            if (scheduledTick < currentServerTick && !CommandValidation.ShiftLateToCurrentTick)
                return false;

            if (scheduledTick < currentServerTick)
                scheduledTick = currentServerTick;

            // Too far future -> clamp or drop
            int maxAllowedFuture = currentServerTick + CommandValidation.MaxFutureTicks;
            if (scheduledTick > maxAllowedFuture && !CommandValidation.ClampFutureToMax)
                return false;

            if (scheduledTick > maxAllowedFuture)
                scheduledTick = maxAllowedFuture;

            EnqueueInternal(connectionId, scheduledTick, clientCmdSeq, commands);
            return true;
        }

        private void EnqueueInternal(int connectionId, int tick, uint clientCmdSeq, ClientCommand[] commands)
        {
            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledBatch>>? byConn))
            {
                byConn = new Dictionary<int, Queue<ScheduledBatch>>(32);
                _byTickByConn.Add(tick, byConn);
            }

            if (!byConn.TryGetValue(connectionId, out Queue<ScheduledBatch>? q))
            {
                q = new Queue<ScheduledBatch>(16);
                byConn.Add(connectionId, q);
            }

            ClientCommand[] copy = CopyCommands(commands);
            ScheduledBatch batch = new ScheduledBatch(tick, clientCmdSeq, copy);
            q.Enqueue(batch);
        }

        public IEnumerable<int> ConnectionIdsForTick(int tick)
        {
            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledBatch>>? byConn))
                yield break;

            foreach (KeyValuePair<int, Queue<ScheduledBatch>> kv in byConn)
                yield return kv.Key;
        }

        public bool TryDequeueForTick(int tick, int connectionId, out ScheduledBatch batch)
        {
            batch = default(ScheduledBatch);

            if (!_byTickByConn.TryGetValue(tick, out Dictionary<int, Queue<ScheduledBatch>>? byConn))
                return false;

            if (!byConn.TryGetValue(connectionId, out Queue<ScheduledBatch>? q))
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

            foreach (KeyValuePair<int, Dictionary<int, Queue<ScheduledBatch>>> kv in _byTickByConn)
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

        private static ClientCommand[] CopyCommands(ClientCommand[] commands)
        {
            ClientCommand[] dst = new ClientCommand[commands.Length];

            int i = 0;
            while (i < commands.Length)
            {
                dst[i] = commands[i];
                i++;
            }

            return dst;
        }

        public readonly struct ScheduledBatch
        {
            public readonly int Tick;
            public readonly uint ClientCmdSeq;
            public readonly ClientCommand[] Commands;

            public ScheduledBatch(int tick, uint clientCmdSeq, ClientCommand[] commands)
            {
                Tick = tick;
                ClientCmdSeq = clientCmdSeq;
                Commands = commands;
            }
        }
    }
}
