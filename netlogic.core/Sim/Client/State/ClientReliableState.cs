using System;

namespace Sim.Client.State
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
