using System;
using System.Collections.Generic;
using com.aqua.netlogic.sim.game.runtime;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.game.entity;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.net;

namespace com.aqua.netlogic.sim.game
{
    /// <summary>
    /// The entry for complete game state
    /// </summary>
    public sealed class ServerModel : IRepOpTarget
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

        public RunRuntime Run { get; } = new RunRuntime();
        public LevelRuntime Level { get; } = new LevelRuntime();
        public RoundRuntime Round { get; } = new RoundRuntime();

        // Controllers are rebuildable logic; not serialized
        internal GameFlowManager FlowManager { get; }
        internal GameFlowController GameFlow { get; }
        internal RoundFlowController RoundFlow { get; }

        public ServerModel()
        {
            FlowManager = new GameFlowManager(this);
            GameFlow = new GameFlowController(this);
            RoundFlow = new RoundFlowController(this);
        }

        // ------------------------------------------------------------------
        // Entities (unchanged)
        // ------------------------------------------------------------------

        public IEnumerable<Entity> Entities => EntityManager.Entities;

        public int AllocateEntityId() => EntityManager.AllocateEntityId();

        public bool TryGetEntity(int id, out Entity entity) =>
            EntityManager.TryGetEntity(id, out entity);

        public EntityState[] ToSnapshot() => EntityManager.ToSnapshot();

        // Compatibility methods for existing ServerSim code
        public bool TryGet(int id, out Entity e) => TryGetEntity(id, out e);

        /// <summary>
        /// Called once per server tick after systems have executed.
        /// </summary>
        public void Advance(int deltaTick)
        {
            CurrentTick += deltaTick;

            // Deterministic lifecycle step (enter/exit hooks, init state)
            FlowManager.LifecycleStep();

            // Put deterministic per-tick world logic here (regen, ai, projectiles, etc.)
        }

        public FlowSnapshot BuildFlowSnapshot()
        {
            // Keep it compact and always safe to read on client.
            return new FlowSnapshot(
                flowState: FlowManager.State,
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

        public void ApplyFlowSnapshot(
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
            FlowManager.SetStateInternal((GameFlowState)flowState);
            Run.SelectedChefHatId = selectedChefHatId;
            Run.LevelIndex = levelIndex;

            Round.RoundIndex = roundIndex;
            Round.TargetScore = targetScore;
            Round.CumulativeScore = cumulativeScore;
            Round.CookAttemptsUsed = cookAttemptsUsed;
            Round.LastCookSeq = cookResultSeq;
            Round.LastCookScoreDelta = lastCookScoreDelta;
            Round.LastCookMetTarget = lastCookMetTarget != 0;
            Round.State = (com.aqua.netlogic.sim.game.flow.RoundState)roundState;
        }

        public void ApplyFlowFire(byte trigger, int param0)
        {
            GameFlowIntent intent = (GameFlowIntent)trigger;
            GameFlow.ApplyPlayerIntentFromCommand(intent, param0);
        }
    }
}
