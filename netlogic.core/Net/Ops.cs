using LiteNetLib.Utils;

namespace Net
{
    public enum OpType : byte
    {
        // Client -> Server (Reliable)
        MoveBy = 1,
        FlowFire = 2,

        // Server -> Client (Unreliable)
        PositionSnapshot = 50,

        // Server -> Client (Reliable)
        FlowSnapshot = 60
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

        // Payload: [byte trigger][3 bytes padding][int param0]  => total 8 bytes
        public static void WriteFlowFire(NetDataWriter w, byte trigger, int param0)
        {
            w.Put((byte)OpType.FlowFire);
            w.Put((ushort)8);

            w.Put(trigger);
            w.Put((byte)0);
            w.Put((byte)0);
            w.Put((byte)0);

            w.Put(param0);
        }

        public static void WritePositionSnapshot(NetDataWriter w, int entityId, int x, int y)
        {
            w.Put((byte)OpType.PositionSnapshot);
            w.Put((ushort)12);
            w.Put(entityId);
            w.Put(x);
            w.Put(y);
        }

        // Payload (32 bytes):
        // [byte flowState][byte roundState][byte lastMetTarget][byte attemptsUsed]
        // [int levelIndex][int roundIndex][int selectedHatId]
        // [int targetScore][int cumulativeScore]
        // [int cookResultSeq][int lastCookScoreDelta]
        public static void WriteFlowSnapshot(
            NetDataWriter w,
            byte flowState,
            byte roundState,
            byte lastMetTarget,
            byte cookAttemptsUsed,
            int levelIndex,
            int roundIndex,
            int selectedChefHatId,
            int targetScore,
            int cumulativeScore,
            int cookResultSeq,
            int lastCookScoreDelta)
        {
            w.Put((byte)OpType.FlowSnapshot);
            w.Put((ushort)32);

            w.Put(flowState);
            w.Put(roundState);
            w.Put(lastMetTarget);
            w.Put(cookAttemptsUsed);

            w.Put(levelIndex);
            w.Put(roundIndex);
            w.Put(selectedChefHatId);
            w.Put(targetScore);
            w.Put(cumulativeScore);
            w.Put(cookResultSeq);
            w.Put(lastCookScoreDelta);
        }
    }

    public static class OpsReader
    {
        public static OpType ReadOpType(NetDataReader r) => (OpType)r.GetByte();

        public static ushort ReadOpLen(NetDataReader r) => r.GetUShort();

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
