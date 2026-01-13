using System;

namespace Net
{
    /// <summary>
    /// Network packet representation with connection ID, lane, and payload bytes.
    /// </summary>
    public readonly struct NetPacket
    {
        public readonly int ConnectionId;
        public readonly Lane Lane;
        public readonly ArraySegment<byte> Payload;

        public NetPacket(int connectionId, Lane lane, ArraySegment<byte> payload)
        {
            ConnectionId = connectionId;
            Lane = lane;
            Payload = payload;
        }
    }
}
