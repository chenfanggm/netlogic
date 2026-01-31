using System.Runtime.CompilerServices;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Authoritative, fixed-width operation record.
    ///
    /// IMPORTANT:
    /// - RepOp is NOT a network packet/frame concept.
    /// - It is a simulation contract record.
    ///
    /// Storage is fixed-width for performance and optional codec friendliness,
    /// but all public accessors are semantic (EntityId, X, Y, etc.).
    /// </summary>
    public readonly struct RepOp
    {
        public readonly RepOpType Type;

        // Fixed-width storage (do not use directly outside this file)
        private readonly int _p0, _p1, _p2, _p3, _p4, _p5, _p6, _p7;

        private RepOp(
            RepOpType type,
            int p0 = 0, int p1 = 0, int p2 = 0, int p3 = 0,
            int p4 = 0, int p5 = 0, int p6 = 0, int p7 = 0)
        {
            Type = type;
            _p0 = p0; _p1 = p1; _p2 = p2; _p3 = p3;
            _p4 = p4; _p5 = p5; _p6 = p6; _p7 = p7;
        }

        // -----------------------
        // Factory constructors
        // -----------------------

        public static RepOp EntitySpawned(int entityId, int x, int y, int hp)
            => new RepOp(RepOpType.EntitySpawned, p0: entityId, p1: x, p2: y, p3: hp);

        public static RepOp EntityDestroyed(int entityId)
            => new RepOp(RepOpType.EntityDestroyed, p0: entityId);

        public static RepOp PositionSnapshot(int entityId, int x, int y)
            => new RepOp(RepOpType.PositionSnapshot, p0: entityId, p1: x, p2: y);

        public static RepOp FlowFire(byte trigger, int param0 = 0)
            => new RepOp(RepOpType.FlowFire, p0: trigger, p1: param0);

        /// <summary>
        /// FlowSnapshot packs 4 bytes into header int:
        /// byte0=flowState, byte1=roundState, byte2=lastCookMetTarget, byte3=cookAttemptsUsed
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
            int header =
                (flowState)
                | (roundState << 8)
                | (lastCookMetTarget << 16)
                | (cookAttemptsUsed << 24);

            return new RepOp(
                RepOpType.FlowSnapshot,
                p0: header,
                p1: levelIndex,
                p2: roundIndex,
                p3: selectedChefHatId,
                p4: targetScore,
                p5: cumulativeScore,
                p6: cookResultSeq,
                p7: lastCookScoreDelta);
        }

        // -----------------------
        // Semantic accessors
        // -----------------------

        public int EntityId => _p0;

        public int X => _p1;
        public int Y => _p2;

        public int Hp => _p3;

        public byte Trigger => (byte)_p0;
        public int Param0 => _p1;

        // FlowSnapshot semantic fields
        public byte FlowState => (byte)(_p0 & 0xFF);
        public byte RoundState => (byte)((_p0 >> 8) & 0xFF);
        public byte LastCookMetTarget => (byte)((_p0 >> 16) & 0xFF);
        public byte CookAttemptsUsed => (byte)((_p0 >> 24) & 0xFF);

        public int LevelIndex => _p1;
        public int RoundIndex => _p2;
        public int SelectedChefHatId => _p3;
        public int TargetScore => _p4;
        public int CumulativeScore => _p5;
        public int CookResultSeq => _p6;
        public int LastCookScoreDelta => _p7;

        // Optional: allow codec layer to access raw ints without exposing names
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetRaw(out int p0, out int p1, out int p2, out int p3, out int p4, out int p5, out int p6, out int p7)
        {
            p0 = _p0; p1 = _p1; p2 = _p2; p3 = _p3;
            p4 = _p4; p5 = _p5; p6 = _p6; p7 = _p7;
        }
    }
}
