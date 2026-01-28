using System;
using System.Collections.Generic;

namespace Sim.Server.Reliability
{
    public sealed class ServerReliableOpLog
    {
        private readonly int _capacity;
        private readonly Queue<Entry> _entries;

        public ServerReliableOpLog(int capacity)
        {
            _capacity = capacity;
            _entries = new Queue<Entry>(capacity);
        }

        public void Add(uint seq, int tick, byte[] packetBytes)
        {
            Entry e = new Entry(seq, tick, packetBytes);
            _entries.Enqueue(e);

            while (_entries.Count > _capacity)
                _entries.Dequeue();
        }

        public IEnumerable<Entry> EntriesAfterSeq(uint lastAckedSeq)
        {
            foreach (Entry e in _entries)
            {
                if (e.Seq > lastAckedSeq)
                    yield return e;
            }
        }

        public readonly struct Entry
        {
            public readonly uint Seq;
            public readonly int Tick;
            public readonly byte[] PacketBytes;

            public Entry(uint seq, int tick, byte[] packetBytes)
            {
                Seq = seq;
                Tick = tick;
                PacketBytes = packetBytes;
            }
        }
    }
}
