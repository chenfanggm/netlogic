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

        // -----------------------
        // Flow + Runtime factories
        // -----------------------

        public static RepOp FlowStateSet(com.aqua.netlogic.sim.game.flow.GameFlowState state)
            => new RepOp(RepOpType.FlowStateSet, p0: (int)state);

        public static RepOp RunReset(uint seed = 1)
            => new RepOp(RepOpType.RunReset, p0: unchecked((int)seed));

        public static RepOp RunSelectedChefHatSet(int chefHatId)
            => new RepOp(RepOpType.RunSelectedChefHatSet, p0: chefHatId);

        public static RepOp RunLevelIndexSet(int levelIndex)
            => new RepOp(RepOpType.RunLevelIndexSet, p0: levelIndex);

        public static RepOp RunSeedSet(uint seed)
            => new RepOp(RepOpType.RunSeedSet, p0: unchecked((int)seed));

        public static RepOp RunRngResetFromSeed(uint seed)
            => new RepOp(RepOpType.RunRngResetFromSeed, p0: unchecked((int)seed));

        public static RepOp LevelReset()
            => new RepOp(RepOpType.LevelReset);

        public static RepOp LevelRefreshesRemainingSet(int refreshesRemaining)
            => new RepOp(RepOpType.LevelRefreshesRemainingSet, p0: refreshesRemaining);

        public static RepOp LevelPendingServeCustomerIndexSet(int index)
            => new RepOp(RepOpType.LevelPendingServeCustomerIndexSet, p0: index);

        public static RepOp LevelCustomerIdSet(int slotIndex, int customerId)
            => new RepOp(RepOpType.LevelCustomerIdSet, p0: slotIndex, p1: customerId);

        public static RepOp LevelCustomerServedSet(int slotIndex, bool served)
            => new RepOp(RepOpType.LevelCustomerServedSet, p0: slotIndex, p1: served ? 1 : 0);

        public static RepOp RoundReset()
            => new RepOp(RepOpType.RoundReset);

        public static RepOp RoundStateSet(com.aqua.netlogic.sim.game.flow.RoundState state)
            => new RepOp(RepOpType.RoundStateSet, p0: (int)state);

        public static RepOp RoundRoundIndexSet(int roundIndex)
            => new RepOp(RepOpType.RoundRoundIndexSet, p0: roundIndex);

        public static RepOp RoundCustomerIdSet(int customerId)
            => new RepOp(RepOpType.RoundCustomerIdSet, p0: customerId);

        public static RepOp RoundTargetScoreSet(int targetScore)
            => new RepOp(RepOpType.RoundTargetScoreSet, p0: targetScore);

        public static RepOp RoundCookAttemptsUsedSet(int used)
            => new RepOp(RepOpType.RoundCookAttemptsUsedSet, p0: used);

        public static RepOp RoundCumulativeScoreSet(int score)
            => new RepOp(RepOpType.RoundCumulativeScoreSet, p0: score);

        public static RepOp RoundLastCookSeqSet(int seq)
            => new RepOp(RepOpType.RoundLastCookSeqSet, p0: seq);

        public static RepOp RoundLastCookScoreDeltaSet(int delta)
            => new RepOp(RepOpType.RoundLastCookScoreDeltaSet, p0: delta);

        public static RepOp RoundLastCookMetTargetSet(bool met)
            => new RepOp(RepOpType.RoundLastCookMetTargetSet, p0: met ? 1 : 0);

        public static RepOp RoundIsRoundWonSet(bool won)
            => new RepOp(RepOpType.RoundIsRoundWonSet, p0: won ? 1 : 0);

        public static RepOp RoundIsRunLostSet(bool lost)
            => new RepOp(RepOpType.RoundIsRunLostSet, p0: lost ? 1 : 0);

        // Buff: remainingTicks (0 means removed)
        public static RepOp EntityBuffSet(int entityId, com.aqua.netlogic.sim.game.rules.BuffType buff, int remainingTicks)
            => new RepOp(RepOpType.EntityBuffSet, p0: entityId, p1: (int)buff, p2: remainingTicks);

        // Cooldown: remainingTicks (0 means ready)
        public static RepOp EntityCooldownSet(int entityId, com.aqua.netlogic.sim.game.rules.CooldownType cd, int remainingTicks)
            => new RepOp(RepOpType.EntityCooldownSet, p0: entityId, p1: (int)cd, p2: remainingTicks);

        // -----------------------
        // Semantic accessors
        // -----------------------

        public int EntityId => _p0;

        public int X => _p1;
        public int Y => _p2;

        public int Hp => _p3;

        // -----------------------
        // Flow + Runtime accessors
        // -----------------------

        public int IntValue0 => _p0;
        public int IntValue1 => _p1;

        public bool BoolValue0 => _p0 != 0;
        public bool BoolValue1 => _p1 != 0;

        public uint UIntValue0 => unchecked((uint)_p0);

        public int SlotIndex => _p0;
        public int CustomerIdValue => _p1;
        public int RemainingTicks => _p2;
        public int KindId => _p1;

        // Optional: allow codec layer to access raw ints without exposing names
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetRaw(out int p0, out int p1, out int p2, out int p3, out int p4, out int p5, out int p6, out int p7)
        {
            p0 = _p0; p1 = _p1; p2 = _p2; p3 = _p3;
            p4 = _p4; p5 = _p5; p6 = _p6; p7 = _p7;
        }
    }
}
