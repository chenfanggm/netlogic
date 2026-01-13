using System;
using System.Collections.Generic;
using System.Diagnostics;
using Net;

namespace Sim
{
    public sealed class PendingBatches
    {
        private readonly Stopwatch _sw;
        private readonly Dictionary<uint, PendingItem> _pending;

        public PendingBatches()
        {
            _sw = Stopwatch.StartNew();
            _pending = new Dictionary<uint, PendingItem>(256);
        }

        public int Count
        {
            get { return _pending.Count; }
        }

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
            PendingItem item;
            if (_pending.TryGetValue(seq, out item))
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

        private readonly struct PendingItem
        {
            public readonly CommandBatchMsg Message;
            public readonly long LastSentAtMs;
            public readonly int SendCount;

            public PendingItem(CommandBatchMsg message, long lastSentAtMs, int sendCount)
            {
                Message = message;
                LastSentAtMs = lastSentAtMs;
                SendCount = sendCount;
            }
        }
    }
}
