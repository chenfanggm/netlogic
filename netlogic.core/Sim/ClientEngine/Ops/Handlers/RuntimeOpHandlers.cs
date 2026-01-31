using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    public sealed class RunResetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RunSelectedChefHatSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RunLevelIndexSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RunSeedSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RunRngResetFromSeedHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class LevelResetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class LevelRefreshesRemainingSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class LevelPendingServeCustomerIndexSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class LevelCustomerIdSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class LevelCustomerServedSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundResetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundStateSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundRoundIndexSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundCustomerIdSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundTargetScoreSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundCookAttemptsUsedSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundCumulativeScoreSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundLastCookSeqSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundLastCookScoreDeltaSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundLastCookMetTargetSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundIsRoundWonSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class RoundIsRunLostSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class EntityBuffSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }

    public sealed class EntityCooldownSetHandler : IClientRepOpHandler
    {
        public void Apply(in ClientOpContext ctx, in RepOp op)
        {
            RepOpApplier.ApplyAuthoritative(ctx.Model, op);
        }
    }
}
