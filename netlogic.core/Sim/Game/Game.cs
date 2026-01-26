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

        public Entity CreateEntityAt(int x, int y) => EntityManager.CreateEntityAt(x, y);

        public Entity CreateEntityAt(int entityId, int x, int y) =>
            EntityManager.CreateEntityAt(entityId, x, y);

        public bool TryGetEntity(int id, out Entity entity) =>
            EntityManager.TryGetEntity(id, out entity);

        public EntityState[] ToSnapshot() => EntityManager.ToSnapshot();

        public bool TryMoveEntityBy(int entityId, int dx, int dy) =>
            EntityManager.TryMoveEntityBy(entityId, dx, dy);

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

        public SampleWorldSnapshot BuildSnapshot()
        {
            List<SampleEntityPos> list = new List<SampleEntityPos>(128);
            foreach (Entity e in Entities)
                list.Add(new SampleEntityPos(e.Id, e.X, e.Y));

            FlowSnapshot flow = BuildFlowSnapshot();
            return new SampleWorldSnapshot(flow, list.ToArray());
        }
    }

    public sealed class Entity
    {
        public int Id { get; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; private set; }

        public Entity(int id, int x, int y)
        {
            Id = id;
            X = x;
            Y = y;
            Hp = 100;
        }

        public void MoveBy(int dx, int dy) => X += dx;

        public void Damage(int amount)
        {
            Hp -= amount;
            if (Hp < 0) Hp = 0;
        }
    }
}
