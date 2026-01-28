using Sim.Game.Runtime;
using Sim.Game.Flow;
using Sim.Snapshot;
using Sim.Engine;
using Net;

namespace Sim.Game
{
    /// <summary>
    /// The entry for complete game state
    /// </summary>
    public sealed class Game
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
        // Transient per-tick output hook (set by engine)
        // ------------------------------------------------------------------
        internal IWorldReplicator? Replicator { get; private set; }

        internal void SetReplicator(IWorldReplicator? replicator)
        {
            Replicator = replicator;
        }


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

        public Game()
        {
            FlowManager = new GameFlowManager(this);
            GameFlow = new GameFlowController(this);
            RoundFlow = new RoundFlowController(this);
        }

        // ------------------------------------------------------------------
        // Entities (unchanged)
        // ------------------------------------------------------------------

        public IEnumerable<Entity> Entities => EntityManager.Entities;

        public Entity CreateEntityAt(int x, int y)
        {
            Entity e = EntityManager.CreateEntityAt(x, y);
            if (Replicator != null)
                Replicator.Record(Sim.Engine.RepOp.EntitySpawned(e.Id, e.X, e.Y, e.Hp));
            return e;
        }

        public Entity CreateEntityAt(int entityId, int x, int y)
        {
            Entity e = EntityManager.CreateEntityAt(entityId, x, y);
            if (Replicator != null)
                Replicator.Record(Sim.Engine.RepOp.EntitySpawned(e.Id, e.X, e.Y, e.Hp));
            return e;
        }

        public bool TryGetEntity(int id, out Entity entity) =>
            EntityManager.TryGetEntity(id, out entity);

        public EntityState[] ToSnapshot() => EntityManager.ToSnapshot();

        public bool TryMoveEntityBy(int entityId, int dx, int dy)
        {
            bool ok = EntityManager.TryMoveEntityBy(entityId, dx, dy, out int newX, out int newY);
            if (ok && Replicator != null)
                Replicator.Record(Sim.Engine.RepOp.PositionSnapshot(entityId, newX, newY));

            return ok;
        }

        public bool TryRemoveEntity(int entityId)
        {
            bool ok = EntityManager.TryRemoveEntity(entityId);
            if (ok && Replicator != null)
                Replicator.Record(Sim.Engine.RepOp.EntityDestroyed(entityId));

            return ok;
        }

        // Compatibility methods for existing ServerSim code
        public Entity Spawn(int x, int y) => CreateEntityAt(x, y);

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

        public GameSnapshot Snapshot()
        {
            List<SampleEntityPos> list = new List<SampleEntityPos>(128);
            foreach (Entity e in Entities)
                list.Add(new SampleEntityPos(e.Id, e.X, e.Y, e.Hp));

            FlowSnapshot flow = BuildFlowSnapshot();
            return new GameSnapshot(flow, list.ToArray());
        }
    }
}
