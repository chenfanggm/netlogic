// FILE: netlogic.core/Sim/Game/Rules/RulesReducer.cs
//
// Full-ops rule evaluation for one tick.
// - This replaces the "game.Advance(1)" style of direct mutation.
// - It MUST NOT mutate ServerModel directly.
// - It MUST ONLY emit RepOps via OpWriter (Emit or EmitAndApply).
//
// Naming intent:
// - "Reducer" = derives state transitions (ops) from current state + rules.
// - This module owns time-based consequences (cooldowns, buffs, heat, timers, etc).
//
// Recommended calling location:
//   ServerEngine.TickOnce:
//     _commandSystem.Execute(tick, world, ops);  // intent -> ops
//     RulesReducer.ApplyTick(tick, world, ops);  // time rules -> ops
//
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.game.rules
{
    public static class RulesReducer
    {
        /// <summary>
        /// Apply all deterministic "time-based" rules for the current tick.
        /// Must be called exactly once per authoritative tick.
        /// </summary>
        public static void ApplyTick(int tick, ServerModel world, OpWriter ops)
        {
            // IMPORTANT:
            // - Keep execution order stable for determinism.
            // - Use EmitAndApply when subsequent rules depend on mutations.
            //
            // Suggested order:
            // 1) timers/cooldowns (mechanical)
            // 2) buffs/debuffs/status effects (may depend on timers)
            // 3) resource decay/regeneration (heat, energy)
            // 4) rule-driven transitions (timeouts, phase expiry)
            //
            // Do NOT put player intent here. That's handled by command handlers.

            CooldownRules.ApplyTick(tick, world, ops);
            BuffRules.ApplyTick(tick, world, ops);
            StatusEffectRules.ApplyTick(tick, world, ops);
            HeatRules.ApplyTick(tick, world, ops);
            TimeoutRules.ApplyTick(tick, world, ops);

            // Optional: validations/invariants (debug only; no mutation).
            #if DEBUG
            // InvariantChecks.AssertWorldConsistent(world);
            #endif
        }
    }

    // -------------------------------------------------------------------------
    // Hooks / modules
    // -------------------------------------------------------------------------
    // Each module:
    // - reads world state
    // - emits authoritative ops
    // - never mutates directly
    //
    // You can split these into separate files later.
    // Keep them nested for now to accelerate refactor iteration.

    internal static class CooldownRules
    {
        public static void ApplyTick(int tick, ServerModel world, OpWriter ops)
        {
            // Example patterns:
            // - Decrement cooldown timers on abilities/items/entities.
            // - When cooldown reaches 0, emit an op like AbilityCooldownReady(entityId, abilityId).
            //
            // Required ops you might add later:
            // - RepOp.CooldownSet(entityId, abilityId, remainingTicks)
            // - RepOp.CooldownExpired(entityId, abilityId)
            //
            // Implementation strategy:
            // - Iterate deterministic entity list order.
            // - For each cooldown field:
            //     if remaining > 0 => remaining-1 => ops.EmitAndApply(...)
            //
            // Pseudocode:
            // foreach (var e in world.Entities.IterateInStableOrder())
            // {
            //     for each cooldown slot:
            //         int rem = e.Cooldowns[i];
            //         if (rem > 0)
            //             ops.EmitAndApply(RepOp.CooldownSet(e.Id, i, rem - 1));
            // }
            _ = tick;
            _ = world;
            _ = ops;
        }
    }

    internal static class BuffRules
    {
        public static void ApplyTick(int tick, ServerModel world, OpWriter ops)
        {
            // Example patterns:
            // - Decrement buff durations.
            // - Expire buffs and emit ops that remove their effects.
            //
            // Required ops you might add later:
            // - RepOp.BuffDurationSet(entityId, buffId, remainingTicks)
            // - RepOp.BuffRemoved(entityId, buffId)
            // - RepOp.StatModifierRemoved(entityId, statId, amount) (if needed)
            //
            // Important: if buffs modify stats, prefer modelling as:
            // - base stats are persistent
            // - modifiers are explicit state updated by ops
            // so client and server stay in lockstep.
            _ = tick;
            _ = world;
            _ = ops;
        }
    }

    internal static class StatusEffectRules
    {
        public static void ApplyTick(int tick, ServerModel world, OpWriter ops)
        {
            // Example patterns:
            // - Poison ticks damage
            // - Burn applies heat or damage
            // - Stun duration decrements
            //
            // Required ops you might add later:
            // - RepOp.EntityHpSet(entityId, newHp)
            // - RepOp.StatusDurationSet(entityId, statusId, remainingTicks)
            // - RepOp.StatusRemoved(entityId, statusId)
            //
            // Use EmitAndApply so later rules see updated HP/status.
            _ = tick;
            _ = world;
            _ = ops;
        }
    }

    internal static class HeatRules
    {
        public static void ApplyTick(int tick, ServerModel world, OpWriter ops)
        {
            // Example patterns:
            // - Heat drifts toward equilibrium each tick
            // - Overheat/undercool thresholds trigger penalties
            //
            // Required ops you might add later:
            // - RepOp.HeatSet(newHeat)
            // - RepOp.HeatStateSet(Overheated/Optimal/TooCold)
            // - RepOp.HeatPenaltyTriggered(type)
            _ = tick;
            _ = world;
            _ = ops;
        }
    }

    internal static class TimeoutRules
    {
        public static void ApplyTick(int tick, ServerModel world, OpWriter ops)
        {
            // Example patterns:
            // - Round timer counts down and triggers flow transitions when expired.
            // - Phase timeout moves game to next flow state.
            //
            // Required ops you might add later:
            // - RepOp.RoundTimerSet(remainingTicks)
            // - RepOp.FlowStateSet(nextState)
            //
            // NOTE:
            // If this triggers flow transitions, prefer calling FlowReducer helpers
            // that emit the correct sequence of ops for enter-state side effects,
            // rather than directly emitting FlowStateSet alone.
            _ = tick;
            _ = world;
            _ = ops;
        }
    }
}
