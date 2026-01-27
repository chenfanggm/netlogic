namespace Sim.Engine
{
    /// <summary>
    /// Render/network-facing op types (NOT internal domain events).
    /// Keep stable; extend conservatively.
    /// </summary>
    public enum RepOpType : byte
    {
        None = 0,

        // Unreliable state snapshot (presentation only).
        // Latest-wins; safe to drop or overwrite.
        // MUST NOT affect simulation truth.
        PositionSnapshot = 50,

        // Reliable lane (authoritative flow/UI control)
        FlowFire = 60,
        FlowSnapshot = 61,
    }

    /// <summary>
    /// Fixed-width replication op for codec friendliness.
    /// A..H are ints so you can pack bytes into A etc.
    /// </summary>
    public readonly struct RepOp
    {
        public readonly RepOpType Type;
        public readonly int A, B, C, D, E, F, G, H;

        public RepOp(
            RepOpType type,
            int a = 0, int b = 0, int c = 0, int d = 0,
            int e = 0, int f = 0, int g = 0, int h = 0)
        {
            Type = type;
            A = a; B = b; C = c; D = d;
            E = e; F = f; G = g; H = h;
        }

        public static RepOp PositionSnapshot(int entityId, int x, int y)
            => new RepOp(RepOpType.PositionSnapshot, a: entityId, b: x, c: y);

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
