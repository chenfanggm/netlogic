using LiteNetLib.Utils;

namespace Net
{
    /// <summary>
    /// Shared op types for client/server ops payloads.
    /// Keep payload encoding stable and deterministic.
    /// </summary>
    public enum OpType : byte
    {
        // Client -> Server (Reliable lane)
        MoveBy = 1,

        // Server -> Client (Sample lane)
        PositionAt = 50
    }

    public static class OpsWriter
    {
        // Client -> Server (Reliable): MoveBy(entityId, dx, dy)
        public static void WriteMoveBy(NetDataWriter w, int entityId, int dx, int dy)
        {
            w.Put((byte)OpType.MoveBy);
            w.Put(entityId);
            w.Put(dx);
            w.Put(dy);
        }

        // Server -> Client (Sample): PositionAt(entityId, x, y)
        public static void WritePositionAt(NetDataWriter w, int entityId, int x, int y)
        {
            w.Put((byte)OpType.PositionAt);
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
    }
}
