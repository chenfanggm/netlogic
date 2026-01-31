using System;
using System.Collections.Generic;
using com.aqua.netlogic.sim.game.runtime;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.game.entity;
using com.aqua.netlogic.sim.game.rules;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.net;

namespace com.aqua.netlogic.sim.game
{
    /// <summary>
    /// Authoritative game state container (FULL-OPS).
    ///
    /// Rules:
    /// - Authoritative state must only change through RepOps applied via RepOpApplier.ApplyAuthoritative().
    /// - ServerEngine will EmitAndApply ops for sequential dependencies.
    /// - No controllers/managers should mutate state outside ops.
    /// </summary>
    public sealed class ServerModel : IAuthoritativeOpTarget
    {
        // ------------------------------------------------------------------
        // Authoritative time
        // ------------------------------------------------------------------
        public int CurrentTick { get; internal set; }

        // ------------------------------------------------------------------
        // Entities
        // ------------------------------------------------------------------
        internal EntityManager EntityManager { get; } = new EntityManager();

        // ------------------------------------------------------------------
        // Authoritative flow state + runtime state containers
        // ------------------------------------------------------------------
        public GameFlowState FlowState { get; set; } = GameFlowState.Boot;

        public RunRuntime Run { get; } = new RunRuntime();
        public LevelRuntime Level { get; } = new LevelRuntime();
        public RoundRuntime Round { get; } = new RoundRuntime();

        public ServerModel()
        {
        }

        // ------------------------------------------------------------------
        // Entities API
        // ------------------------------------------------------------------

        public IEnumerable<Entity> Entities => EntityManager.Entities;

        public IEnumerable<Entity> IterateEntitiesStable() => EntityManager.Entities;

        public int AllocateEntityId() => EntityManager.AllocateEntityId();

        public bool TryGetEntity(int id, out Entity entity) =>
            EntityManager.TryGetEntity(id, out entity);

        public EntityState[] ToSnapshot() => EntityManager.ToSnapshot();

        // Compatibility methods for existing code
        public bool TryGet(int id, out Entity e) => TryGetEntity(id, out e);

        // ------------------------------------------------------------------
        // Snapshot (baseline/debug tools)
        // ------------------------------------------------------------------

        internal FlowSnapshot BuildFlowSnapshot()
        {
            return new FlowSnapshot(
                flowState: FlowState,
                levelIndex: Run.LevelIndex,
                roundIndex: Round.RoundIndex,
                selectedChefHatId: Run.SelectedChefHatId,
                targetScore: Round.TargetScore,
                cumulativeScore: Round.CumulativeScore,
                cookAttemptsUsed: Round.CookAttemptsUsed,
                roundState: Round.State,
                cookResultSeq: Round.LastCookSeq,
                lastCookScoreDelta: Round.LastCookScoreDelta,
                lastCookMetTarget: Round.LastCookMetTarget);
        }

        public ServerModelSnapshot Snapshot()
        {
            uint stateHash = ServerModelHash.Compute(this);
            return Snapshot(CurrentTick, stateHash);
        }

        public ServerModelSnapshot Snapshot(uint stateHash)
        {
            return Snapshot(CurrentTick, stateHash);
        }

        public ServerModelSnapshot Snapshot(int serverTick, uint stateHash)
        {
            List<SampleEntityPos> list = new List<SampleEntityPos>(128);
            foreach (Entity e in Entities)
                list.Add(new SampleEntityPos(e.Id, e.X, e.Y, e.Hp));

            FlowSnapshot flow = BuildFlowSnapshot();
            return new ServerModelSnapshot(flow, list.ToArray(), serverTick, stateHash);
        }

        // ------------------------------------------------------------------
        // Authoritative op application
        // ------------------------------------------------------------------

        public void ApplyEntitySpawned(int entityId, int x, int y, int hp)
        {
            EntityManager.CreateEntityWithId(entityId, x, y, hp);
        }

        public void ApplyEntityDestroyed(int entityId)
        {
            EntityManager.TryRemoveEntity(entityId);
        }

        public void ApplyPositionSnapshot(int entityId, int x, int y)
        {
            EntityManager.SetEntityPosition(entityId, x, y);
        }

        public void ApplyEntityBuffSet(int entityId, BuffType buff, int remainingTicks)
        {
            if (!EntityManager.TryGetEntity(entityId, out Entity entity))
                return;

            switch (buff)
            {
                case BuffType.Haste:
                    entity.HasteTicksRemaining = remainingTicks;
                    return;

                default:
                    return;
            }
        }

        public void ApplyEntityCooldownSet(int entityId, CooldownType cd, int remainingTicks)
        {
            if (!EntityManager.TryGetEntity(entityId, out Entity entity))
                return;

            switch (cd)
            {
                case CooldownType.Dash:
                    entity.DashCooldownTicksRemaining = remainingTicks;
                    return;

                default:
                    return;
            }
        }
    }
}
