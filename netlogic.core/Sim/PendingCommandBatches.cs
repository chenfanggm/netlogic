using System;
using System.Collections.Generic;
using System.Diagnostics;
using Net;

namespace Sim
{
    /// <summary>
    /// Tracks unacknowledged command batches and manages resend logic for reliable delivery.
    /// </summary>
    public sealed class PendingCommandBatches
    {
        public int Count => _pending.Count;

        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly Dictionary<uint, PendingItem> _pending = new Dictionary<uint, PendingItem>(256);

        public void Add(CommandBatchMsg msg)
        {
            long nowMs = _sw.ElapsedMilliseconds;

            PendingItem item = new PendingItem(msg, nowMs, sendCount: 0);
            _pending[msg.ClientSeq] = item;
        }

        public void Ack(uint seq)
        {
            _pending.Remove(seq);
        }

        public void MarkSent(uint seq)
        {
            if (_pending.TryGetValue(seq, out PendingItem item))
            {
                long nowMs = _sw.ElapsedMilliseconds;
                PendingItem updated = new PendingItem(item.Message, nowMs, item.SendCount + 1);
                _pending[seq] = updated;
            }
        }

        public List<CommandBatchMsg> CollectResends(long resendIntervalMs, int maxResendsPerPump)
        {
            long nowMs = _sw.ElapsedMilliseconds;

            List<CommandBatchMsg> resends = new List<CommandBatchMsg>(maxResendsPerPump);
            int added = 0;

            foreach (KeyValuePair<uint, PendingItem> kv in _pending)
            {
                PendingItem item = kv.Value;
                long ageMs = nowMs - item.LastSentAtMs;

                if (ageMs >= resendIntervalMs)
                {
                    resends.Add(item.Message);
                    added++;
                    if (added >= maxResendsPerPump)
                        break;
                }
            }

            return resends;
        }

        /// <summary>
        /// Internal storage for a pending command batch with send tracking.
        /// </summary>
        private readonly struct PendingItem(CommandBatchMsg message, long lastSentAtMs, int sendCount)
        {
            public readonly CommandBatchMsg Message = message;
            public readonly long LastSentAtMs = lastSentAtMs;
            public readonly int SendCount = sendCount;
        }
    }
}
