using System;

namespace com.aqua.netlogic.net
{
    /// <summary>
    /// Network packet representation with connection ID, lane, and payload bytes.
    /// </summary>
    public readonly struct NetPacket
    {
        public readonly int ConnId;
        public readonly Lane Lane;
        public readonly ArraySegment<byte> Payload;

        public NetPacket(int connectionId, Lane lane, ArraySegment<byte> payload)
        {
            ConnId = connectionId;
            Lane = lane;
            Payload = payload;
        }
    }
}
