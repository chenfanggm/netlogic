using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.runtime;
using com.aqua.netlogic.sim.game.rules;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// The ONLY place that interprets RepOps.
    /// Authoritative ops mutate IAuthoritativeOpTarget (server + client).
    /// </summary>
    internal static class RepOpApplier
    {
        public static void ApplyAuthoritative(IAuthoritativeOpTarget target, in RepOp op)
        {
            switch (op.Type)
            {
                case RepOpType.EntitySpawned:
                    target.ApplyEntitySpawned(op.EntityId, op.X, op.Y, op.Hp);
                    return;

                case RepOpType.EntityDestroyed:
                    target.ApplyEntityDestroyed(op.EntityId);
                    return;

                case RepOpType.PositionSnapshot:
                    target.ApplyPositionSnapshot(op.EntityId, op.X, op.Y);
                    return;

                case RepOpType.EntityBuffSet:
                    target.ApplyEntityBuffSet(op.EntityId, (BuffType)op.KindId, op.RemainingTicks);
                    return;

                case RepOpType.EntityCooldownSet:
                    target.ApplyEntityCooldownSet(op.EntityId, (CooldownType)op.KindId, op.RemainingTicks);
                    return;
            }

            ApplyRuntime(target, op);
        }

        public static void ApplyRuntime(IRuntimeOpTarget t, in RepOp op)
        {
            switch (op.Type)
            {
                case RepOpType.FlowStateSet:
                    t.FlowState = (GameFlowState)op.IntValue0;
                    return;

                // --------------------
                // RunRuntime
                // --------------------
                case RepOpType.RunReset:
                    t.Run.SelectedChefHatId = 0;
                    t.Run.LevelIndex = 0;
                    t.Run.RunSeed = 0;
                    t.Run.Rng = new DeterministicRng(op.UIntValue0 == 0 ? 1u : op.UIntValue0);
                    return;

                case RepOpType.RunSelectedChefHatSet:
                    t.Run.SelectedChefHatId = op.IntValue0;
                    return;

                case RepOpType.RunLevelIndexSet:
                    t.Run.LevelIndex = op.IntValue0;
                    return;

                case RepOpType.RunSeedSet:
                    t.Run.RunSeed = op.UIntValue0;
                    return;

                case RepOpType.RunRngResetFromSeed:
                    t.Run.RunSeed = op.UIntValue0;
                    t.Run.Rng = new DeterministicRng(t.Run.RunSeed);
                    return;

                // --------------------
                // LevelRuntime
                // --------------------
                case RepOpType.LevelReset:
                    t.Level.ResetForNewLevel();
                    return;

                case RepOpType.LevelRefreshesRemainingSet:
                    t.Level.RefreshesRemaining = op.IntValue0;
                    return;

                case RepOpType.LevelPendingServeCustomerIndexSet:
                    t.Level.PendingServeCustomerIndex = op.IntValue0;
                    return;

                case RepOpType.LevelCustomerIdSet:
                    {
                        int slot = op.SlotIndex;
                        int[] ids = t.Level.CustomerIdsMutable;
                        if ((uint)slot < (uint)ids.Length)
                            ids[slot] = op.CustomerIdValue;
                        return;
                    }

                case RepOpType.LevelCustomerServedSet:
                    {
                        int slot = op.SlotIndex;
                        bool[] served = t.Level.ServedMutable;
                        if ((uint)slot < (uint)served.Length)
                            served[slot] = op.IntValue1 != 0;
                        return;
                    }

                // --------------------
                // RoundRuntime
                // --------------------
                case RepOpType.RoundReset:
                    t.Round.ResetForNewRound();
                    return;

                case RepOpType.RoundStateSet:
                    t.Round.State = (RoundState)op.IntValue0;
                    return;

                case RepOpType.RoundRoundIndexSet:
                    t.Round.RoundIndex = op.IntValue0;
                    return;

                case RepOpType.RoundCustomerIdSet:
                    t.Round.CustomerId = op.IntValue0;
                    return;

                case RepOpType.RoundTargetScoreSet:
                    t.Round.TargetScore = op.IntValue0;
                    return;

                case RepOpType.RoundCookAttemptsUsedSet:
                    t.Round.CookAttemptsUsed = op.IntValue0;
                    return;

                case RepOpType.RoundCumulativeScoreSet:
                    t.Round.CumulativeScore = op.IntValue0;
                    return;

                case RepOpType.RoundLastCookSeqSet:
                    t.Round.LastCookSeq = op.IntValue0;
                    return;

                case RepOpType.RoundLastCookScoreDeltaSet:
                    t.Round.LastCookScoreDelta = op.IntValue0;
                    return;

                case RepOpType.RoundLastCookMetTargetSet:
                    t.Round.LastCookMetTarget = op.BoolValue0;
                    return;

                case RepOpType.RoundIsRoundWonSet:
                    t.Round.IsRoundWon = op.BoolValue0;
                    return;

                case RepOpType.RoundIsRunLostSet:
                    t.Round.IsRunLost = op.BoolValue0;
                    return;
            }
        }
    }
}
