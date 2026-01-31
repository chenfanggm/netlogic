using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.runtime;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.game.flow
{
    /// <summary>
    /// Full-ops replacement for:
    /// - GameFlowController (Stateless transitions + intent validation)
    /// - GameFlowManager (enter-state hooks / lifecycle step)
    /// - RoundFlowController (cook/continue rules)
    ///
    /// Rules:
    /// - Must NOT mutate ServerModel directly.
    /// - Must ONLY emit ops (and server may EmitAndApply for sequential dependency).
    /// - Must be deterministic.
    /// </summary>
    internal static class FlowReducer
    {
        public static void ApplyPlayerIntent(ServerModel world, OpWriter ops, GameFlowIntent intent, int param0)
        {
            if (intent == GameFlowIntent.None)
                return;

            GameFlowState state = world.FlowState;

            // -----------------------------
            // Round-local intents (no top-level transition required)
            // Mirrors GameFlowController's special-case handling when InRound.
            // -----------------------------
            if (state == GameFlowState.InRound)
            {
                if (intent == GameFlowIntent.ClickCook)
                {
                    ApplyCook(world, ops);
                    return;
                }

                if (intent == GameFlowIntent.ClickContinue)
                {
                    ApplyContinue(world, ops);
                    return;
                }
            }

            // -----------------------------
            // Top-level transitions and payload writes
            // Mirrors GameFlowController + GameFlowManager enter hooks.
            // -----------------------------
            switch (intent)
            {
                case GameFlowIntent.ReturnToMenu:
                    TransitionTo(world, ops, GameFlowState.MainMenu);
                    return;

                case GameFlowIntent.ClickNewGame:
                    if (state == GameFlowState.MainMenu)
                        TransitionTo(world, ops, GameFlowState.RunSetup);
                    return;

                case GameFlowIntent.SelectChefHat:
                    if (state == GameFlowState.RunSetup)
                        ops.EmitAndApply(RepOp.RunSelectedChefHatSet(param0)); // reentry allowed
                    return;

                case GameFlowIntent.ClickStartRun:
                    // Validate: requires hat selected (matches GameFlowController)
                    if (state == GameFlowState.RunSetup && world.Run.SelectedChefHatId != 0)
                        TransitionTo(world, ops, GameFlowState.LevelOverview);
                    return;

                case GameFlowIntent.ClickServeCustomer:
                    if (state == GameFlowState.LevelOverview)
                    {
                        // payload write (matches GameFlowController)
                        ops.EmitAndApply(RepOp.LevelPendingServeCustomerIndexSet(param0));
                        TransitionTo(world, ops, GameFlowState.InRound);
                    }
                    return;

                case GameFlowIntent.ClickConcedeRun:
                    if (state == GameFlowState.LevelOverview || state == GameFlowState.InRound)
                        TransitionTo(world, ops, GameFlowState.RunDefeat);
                    return;

                default:
                    return;
            }
        }

        // ------------------------------------------------------------
        // Transition + Enter-state hooks (replaces GameFlowManager)
        // ------------------------------------------------------------

        private static void TransitionTo(ServerModel world, OpWriter ops, GameFlowState next)
        {
            // In current code, FlowManager handles OnEnter on the next tick in Advance().
            // In full-ops we do it immediately via emitted ops.
            ops.EmitAndApply(RepOp.FlowStateSet(next));
            EnterState(world, ops, next);
        }

        private static void EnterState(ServerModel world, OpWriter ops, GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Boot:
                    return;

                case GameFlowState.MainMenu:
                    ResetAllRuntime(world, ops);
                    return;

                case GameFlowState.RunSetup:
                    ResetAllRuntime(world, ops);
                    return;

                case GameFlowState.LevelOverview:
                    EnsureLevelInitialized(world, ops);
                    return;

                case GameFlowState.InRound:
                    InitRoundFromPendingServe(world, ops);
                    return;

                case GameFlowState.RunDefeat:
                case GameFlowState.RunVictory:
                default:
                    return;
            }
        }

        private static void ResetAllRuntime(ServerModel world, OpWriter ops)
        {
            // Mirrors GameFlowManager.ResetAllRuntime()
            // SelectedChefHatId=0; LevelIndex=0; RunSeed=0; Rng=DeterministicRng(1)
            ops.EmitAndApply(RepOp.RunSelectedChefHatSet(0));
            ops.EmitAndApply(RepOp.RunLevelIndexSet(0));
            ops.EmitAndApply(RepOp.RunSeedSet(0));
            ops.EmitAndApply(RepOp.RunRngResetFromSeed(1));

            ops.EmitAndApply(RepOp.LevelReset());
            ops.EmitAndApply(RepOp.RoundReset());

            _ = world;
        }

        private static void EnsureLevelInitialized(ServerModel world, OpWriter ops)
        {
            // Mirrors GameFlowManager.EnsureLevelInitialized()

            // First entry into a run:
            if (world.Run.LevelIndex <= 0)
            {
                ops.EmitAndApply(RepOp.RunLevelIndexSet(1));

                // Existing code uses: RunSeed = (uint)(CurrentTick + 12345)
                uint seed = unchecked((uint)(world.CurrentTick + 12345));
                ops.EmitAndApply(RepOp.RunRngResetFromSeed(seed)); // sets seed + rng deterministically
            }

            bool needsCustomers =
                world.Level.CustomerIds[0] == 0 ||
                world.Level.CustomerIds[1] == 0 ||
                world.Level.CustomerIds[2] == 0;

            if (!needsCustomers)
                return;

            ops.EmitAndApply(RepOp.LevelRefreshesRemainingSet(3));

            // Deterministic customer IDs (same formula as current):
            // id = 1000 + levelIndex * 10 + rng.NextInt(0, 10)
            for (int i = 0; i < 3; i++)
            {
                int id = 1000 + world.Run.LevelIndex * 10 + world.Run.Rng.NextInt(0, 10);
                ops.EmitAndApply(RepOp.LevelCustomerIdSet(i, id));
                ops.EmitAndApply(RepOp.LevelCustomerServedSet(i, served: false));
            }

            ops.EmitAndApply(RepOp.LevelPendingServeCustomerIndexSet(-1));
        }

        private static void InitRoundFromPendingServe(ServerModel world, OpWriter ops)
        {
            // Mirrors GameFlowManager.InitRoundFromPendingServe()

            int idx = world.Level.PendingServeCustomerIndex;

            // Validate serve index
            if (idx < 0 || idx >= 3)
                idx = FirstUnservedCustomerIndexOrMinus1(world);

            if (idx < 0)
            {
                // No available customer: fallback to LevelOverview (current code)
                TransitionTo(world, ops, GameFlowState.LevelOverview);
                return;
            }

            // Mark served and clear pending
            ops.EmitAndApply(RepOp.LevelCustomerServedSet(idx, served: true));
            ops.EmitAndApply(RepOp.LevelPendingServeCustomerIndexSet(-1));

            // Init round runtime
            ops.EmitAndApply(RepOp.RoundReset());

            // After serving, served count is 1..3 (use current state after applying served flag)
            int roundIndex = world.Level.ServedCount();
            ops.EmitAndApply(RepOp.RoundRoundIndexSet(roundIndex));

            int customerId = world.Level.CustomerIds[idx];
            ops.EmitAndApply(RepOp.RoundCustomerIdSet(customerId));

            int target = ComputeTargetScore(world.Run.LevelIndex, roundIndex);
            ops.EmitAndApply(RepOp.RoundTargetScoreSet(target));

            ops.EmitAndApply(RepOp.RoundStateSet(RoundState.Prepare));
        }

        private static int FirstUnservedCustomerIndexOrMinus1(ServerModel world)
        {
            for (int i = 0; i < 3; i++)
                if (!world.Level.Served[i]) return i;
            return -1;
        }

        private static int ComputeTargetScore(int levelIndex, int roundIndex)
        {
            // Same placeholder curve as current GameFlowManager
            int baseTarget = 120 + (levelIndex - 1) * 25;
            int roundAdd = roundIndex switch
            {
                1 => 0,
                2 => 40,
                3 => 80,
                _ => 0
            };
            return baseTarget + roundAdd;
        }

        // ------------------------------------------------------------
        // Round rules (replaces RoundFlowController)
        // ------------------------------------------------------------

        private static bool CanCook(ServerModel world)
        {
            return world.FlowState == GameFlowState.InRound
                   && world.Round.State == RoundState.Prepare
                   && !world.Round.IsRunLost
                   && !world.Round.IsRoundWon;
        }

        private static void ApplyCook(ServerModel world, OpWriter ops)
        {
            if (!CanCook(world))
                return;

            // Consume one cook attempt
            int attempts = world.Round.CookAttemptsUsed + 1;
            ops.EmitAndApply(RepOp.RoundCookAttemptsUsedSet(attempts));

            // Placeholder scoring (same as current RoundFlowController)
            int scoreDelta = 50;
            ops.EmitAndApply(RepOp.RoundLastCookScoreDeltaSet(scoreDelta));

            int newCum = world.Round.CumulativeScore + scoreDelta;
            ops.EmitAndApply(RepOp.RoundCumulativeScoreSet(newCum));

            int seq = world.Round.LastCookSeq + 1;
            ops.EmitAndApply(RepOp.RoundLastCookSeqSet(seq));

            bool met = newCum >= world.Round.TargetScore;
            ops.EmitAndApply(RepOp.RoundLastCookMetTargetSet(met));

            // Outcome logic (same as current)
            if (met)
            {
                ops.EmitAndApply(RepOp.RoundIsRoundWonSet(true));
                ops.EmitAndApply(RepOp.RoundStateSet(RoundState.OutcomeReady));
                return;
            }

            if (attempts >= 5)
            {
                ops.EmitAndApply(RepOp.RoundIsRunLostSet(true));
                ops.EmitAndApply(RepOp.RoundStateSet(RoundState.OutcomeReady));
                return;
            }

            // Attempts remain; wait for Continue
            ops.EmitAndApply(RepOp.RoundStateSet(RoundState.OutcomeReady));
        }

        private static void ApplyContinue(ServerModel world, OpWriter ops)
        {
            if (world.FlowState != GameFlowState.InRound)
                return;

            if (world.Round.State != RoundState.OutcomeReady)
                return;

            if (world.Round.IsRunLost)
            {
                // Current code fires ClickConcedeRun -> RunDefeat
                TransitionTo(world, ops, GameFlowState.RunDefeat);
                return;
            }

            if (world.Round.IsRoundWon)
            {
                // Current harness behavior: any won round exits to RunVictory immediately
                TransitionTo(world, ops, GameFlowState.RunVictory);
                return;
            }

            // Not won, attempts remain: go back to prepare for next cook
            ops.EmitAndApply(RepOp.RoundStateSet(RoundState.Prepare));
        }
    }
}
