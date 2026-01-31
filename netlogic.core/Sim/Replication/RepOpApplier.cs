namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// The ONLY place that interprets RepOps and mutates a model.
    /// Shared by ServerModel and ClientModel via IRepOpTarget.
    /// </summary>
    public static class RepOpApplier
    {
        public static void Apply(IRepOpTarget target, in RepOp op)
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

                case RepOpType.FlowFire:
                    target.ApplyFlowFire(op.Trigger, op.Param0);
                    return;

                case RepOpType.FlowSnapshot:
                    target.ApplyFlowSnapshot(
                        op.FlowState,
                        op.RoundState,
                        op.LastCookMetTarget,
                        op.CookAttemptsUsed,
                        op.LevelIndex,
                        op.RoundIndex,
                        op.SelectedChefHatId,
                        op.TargetScore,
                        op.CumulativeScore,
                        op.CookResultSeq,
                        op.LastCookScoreDelta);
                    return;

                default:
                    return;
            }
        }
    }
}
