using System;
using Net;
using Sim.Snapshot;

namespace Client.Protocol
{
    public sealed class ServerSnapshot
    {
        public int ServerTick;
        public uint StateHash;
        public EntityState[] Entities = Array.Empty<EntityState>();
        public FlowSnapshot Flow;
    }
}
