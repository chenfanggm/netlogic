namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Fixed-width replication op for codec friendliness.
    /// A..H are ints so you can pack bytes into A etc.
    /// </summary>
    public readonly struct RepOp(
        RepOpType type,
        int a = 0, int b = 0, int c = 0, int d = 0,
        int e = 0, int f = 0, int g = 0, int h = 0)
    {
        public readonly RepOpType Type = type;
        public readonly int A = a, B = b, C = c, D = d, E = e, F = f, G = g, H = h;

        public static RepOp PositionSnapshot(int entityId, int x, int y)
            => new RepOp(RepOpType.PositionSnapshot, a: entityId, b: x, c: y);

        public static RepOp EntitySpawned(int entityId, int x, int y, int hp)
            => new RepOp(RepOpType.EntitySpawned, a: entityId, b: x, c: y, d: hp);

        public static RepOp EntityDestroyed(int entityId)
            => new RepOp(RepOpType.EntityDestroyed, a: entityId);

        public static RepOp FlowFire(byte trigger, int param0 = 0)
            => new RepOp(RepOpType.FlowFire, a: trigger, b: param0);

        /// <summary>
        /// Flow snapshot encoding:
        /// A packs bytes: [flowState][roundState][lastCookMetTarget][cookAttemptsUsed]
        /// B..H are ints.
        /// </summary>
        public static RepOp FlowSnapshot(
            byte flowState,
            byte roundState,
            byte lastCookMetTarget,
            byte cookAttemptsUsed,
            int levelIndex,
            int roundIndex,
            int selectedChefHatId,
            int targetScore,
            int cumulativeScore,
            int cookResultSeq,
            int lastCookScoreDelta)
        {
            int a = (flowState)
                    | (roundState << 8)
                    | (lastCookMetTarget << 16)
                    | (cookAttemptsUsed << 24);

            return new RepOp(
                RepOpType.FlowSnapshot,
                a: a,
                b: levelIndex,
                c: roundIndex,
                d: selectedChefHatId,
                e: targetScore,
                f: cumulativeScore,
                g: cookResultSeq,
                h: lastCookScoreDelta);
        }
    }
}
