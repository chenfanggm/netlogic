using System;

namespace Sim
{
    public sealed class ClientReliableState
    {
        public uint LastAckedReliableSeq;
        public int LastSeenTick;

        public ClientReliableState()
        {
            LastAckedReliableSeq = 0;
            LastSeenTick = 0;
        }
    }
}
