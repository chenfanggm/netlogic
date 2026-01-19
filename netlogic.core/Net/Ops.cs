using LiteNetLib.Utils;

namespace Net
{
    public enum OpType : byte
    {
        // Client -> Server (Reliable)
        MoveBy = 1,
        FlowFire = 2,

        // Server -> Client (Sample)
        PositionAt = 50
    }

    public static class OpsWriter
    {
        // Op format: [byte opType][ushort opLen][payload...]
        public static void WriteMoveBy(NetDataWriter w, int entityId, int dx, int dy)
        {
            w.Put((byte)OpType.MoveBy);
            w.Put((ushort)12);
            w.Put(entityId);
            w.Put(dx);
            w.Put(dy);
        }

        // Payload: [byte trigger][3 bytes padding]
        // We keep a fixed op length (4) for simplicity and forward compatibility.
        public static void WriteFlowFire(NetDataWriter w, byte trigger)
        {
            w.Put((byte)OpType.FlowFire);
            w.Put((ushort)4);
            w.Put(trigger);
            w.Put((byte)0);
            w.Put((byte)0);
            w.Put((byte)0);
        }

        public static void WritePositionAt(NetDataWriter w, int entityId, int x, int y)
        {
            w.Put((byte)OpType.PositionAt);
            w.Put((ushort)12);
            w.Put(entityId);
            w.Put(x);
            w.Put(y);
        }
    }

    public static class OpsReader
    {
        public static OpType ReadOpType(NetDataReader r)
        {
            return (OpType)r.GetByte();
        }

        public static ushort ReadOpLen(NetDataReader r)
        {
            return r.GetUShort();
        }

        public static void SkipBytes(NetDataReader r, int len)
        {
            int i = 0;
            while (i < len)
            {
                r.GetByte();
                i++;
            }
        }
    }
}
