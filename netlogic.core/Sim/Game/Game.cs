using Sim.Game.Runtime;
using Sim.Game.Flow;
using Sim.Snapshot;
using Sim.Engine;
using Net;

namespace Sim.Game
{
    /// <summary>
    /// Game world that manages entities and provides deterministic simulation state.
    /// </summary>
    public sealed class Game
    {
        private int _nextEntityId = 1;
        private readonly Dictionary<int, Entity> _entities = new Dictionary<int, Entity>(128);

        // ------------------------------------------------------------------
        // Authoritative time
        // ------------------------------------------------------------------

        public int CurrentTick { get; internal set; }

        // ------------------------------------------------------------------
        // Authoritative flow state + runtime state containers
        // ------------------------------------------------------------------

        public GameFlowState FlowState { get; private set; } = GameFlowState.Boot;

        public RunRuntime Run { get; } = new RunRuntime();
        public LevelRuntime Level { get; } = new LevelRuntime();
        public RoundRuntime Round { get; } = new RoundRuntime();

        // Controllers are rebuildable logic; not serialized
        internal GameFlowController Flow { get; }
        internal RoundFlowController RoundFlow { get; }

        // Lifecycle tracking (deterministic, internal)
        private GameFlowState _prevFlowState = (GameFlowState)255;

        public Game()
        {
            Flow = new GameFlowController(this);
            RoundFlow = new RoundFlowController(this);
        }

        internal void SetFlowStateInternal(GameFlowState state)
        {
            FlowState = state;
        }

        // ------------------------------------------------------------------
        // Deterministic lifecycle: runs every tick, reacts to FlowState changes
        // ------------------------------------------------------------------

        private void LifecycleStep()
        {
            if (_prevFlowState != FlowState)
            {
                OnExitFlowState(_prevFlowState);
                OnEnterFlowState(FlowState);
                _prevFlowState = FlowState;
            }

            // You can add other deterministic per-tick checks here if needed.
            // For example: auto-advance level when all 3 customers served and last round won.
        }

        private void OnExitFlowState(GameFlowState state)
        {
            // Keep exit actions minimal; most work should be on enter.
            _ = state;
        }

        private void OnEnterFlowState(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Boot:
                    // No work. We usually transition to MainMenu by client or by a boot command.
                    break;

                case GameFlowState.MainMenu:
                    // Reset everything for a clean slate
                    ResetAllRuntime();
                    break;

                case GameFlowState.RunSetup:
                    // Prepare a fresh run setup selection screen.
                    ResetAllRuntime();
                    // Player will choose hat -> Run.SelectedChefHatId
                    break;

                case GameFlowState.LevelOverview:
                    // If this is a new run start or a new level, ensure level data exists.
                    // We (re)build level preview customers only when needed.
                    EnsureLevelInitialized();
                    break;

                case GameFlowState.InRound:
                    // When player clicks Serve, Level.PendingServeCustomerIndex is set.
                    InitRoundFromPendingServe();
                    break;

                case GameFlowState.RunDefeat:
                    // Nothing required; UI will show defeat.
                    break;

                case GameFlowState.RunVictory:
                    // Nothing required; UI will show victory.
                    break;

                default:
                    break;
            }
        }

        private void ResetAllRuntime()
        {
            Run.SelectedChefHatId = 0;
            Run.LevelIndex = 0;
            Run.RunSeed = 0;
            Run.Rng = new DeterministicRng(1);

            Level.ResetForNewLevel();
            Round.ResetForNewRound();
        }

        private void EnsureLevelInitialized()
        {
            // If level index is 0, this is the first time entering a run.
            if (Run.LevelIndex <= 0)
            {
                Run.LevelIndex = 1;

                // Seed can be derived deterministically; here we use tick as a placeholder.
                // In production you probably set seed when starting the run (ClickStartRun).
                Run.RunSeed = (uint)(CurrentTick + 12345);
                Run.Rng = new DeterministicRng(Run.RunSeed);
            }

            // If customer preview not set, generate 3 customers deterministically
            bool needsCustomers = Level.CustomerIds[0] == 0 || Level.CustomerIds[1] == 0 || Level.CustomerIds[2] == 0;
            if (needsCustomers)
            {
                // Refresh pool per level — tune this
                Level.RefreshesRemaining = 3;

                // Deterministic customer IDs. Replace with weighted roll from data.
                for (int i = 0; i < 3; i++)
                {
                    // Example: base 1000 + level bias + rng
                    int id = 1000 + Run.LevelIndex * 10 + Run.Rng.NextInt(0, 10);
                    Level.CustomerIds[i] = id;
                    Level.Served[i] = false;
                }

                Level.PendingServeCustomerIndex = -1;
            }
        }

        private void InitRoundFromPendingServe()
        {
            int idx = Level.PendingServeCustomerIndex;

            // Validate serve index
            if (idx < 0 || idx >= 3)
            {
                // Invalid selection; fallback: choose first unserved
                idx = FirstUnservedCustomerIndexOrMinus1();
            }

            if (idx < 0)
            {
                // No available customer; treat as level completion placeholder.
                // For now, just keep LevelOverview.
                SetFlowStateInternal(GameFlowState.LevelOverview);
                return;
            }

            // Mark served and clear pending
            Level.Served[idx] = true;
            Level.PendingServeCustomerIndex = -1;

            // Initialize round runtime
            Round.ResetForNewRound();

            Round.RoundIndex = Level.ServedCount(); // after marking served, count is 1..3
            Round.CustomerId = Level.CustomerIds[idx];

            // Target curve placeholder: tune based on your difficulty system
            Round.TargetScore = ComputeTargetScore(Run.LevelIndex, Round.RoundIndex);

            Round.State = RoundState.Prepare;
        }

        private int FirstUnservedCustomerIndexOrMinus1()
        {
            for (int i = 0; i < 3; i++)
                if (!Level.Served[i]) return i;
            return -1;
        }

        private static int ComputeTargetScore(int levelIndex, int roundIndex)
        {
            // Placeholder difficulty curve:
            // Round 1 easy, Round 2 medium, Round 3 boss
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

        // ------------------------------------------------------------------
        // Entities (unchanged)
        // ------------------------------------------------------------------

        public IEnumerable<Entity> Entities
        {
            get
            {
                IEnumerable<int> keys = _entities.Keys.OrderBy(x => x);
                foreach (int k in keys)
                    yield return _entities[k];
            }
        }

        public Entity CreateEntityAt(int x, int y)
        {
            int id = _nextEntityId++;
            Entity e = new Entity(id, x, y);
            _entities.Add(id, e);
            return e;
        }

        public Entity CreateEntityAt(int entityId, int x, int y)
        {
            if (_entities.ContainsKey(entityId))
                return _entities[entityId];

            if (_nextEntityId <= entityId)
                _nextEntityId = entityId + 1;

            Entity e = new Entity(entityId, x, y);
            _entities.Add(entityId, e);
            return e;
        }

        public bool TryGetEntity(int id, out Entity entity)
        {
            return _entities.TryGetValue(id, out entity!);
        }

        public EntityState[] ToSnapshot()
        {
            List<EntityState> list = new List<EntityState>(_entities.Count);

            IEnumerable<int> keys = _entities.Keys.OrderBy(x => x);
            foreach (int id in keys)
            {
                Entity e = _entities[id];
                list.Add(new EntityState(e.Id, e.X, e.Y, e.Hp));
            }

            return list.ToArray();
        }

        public bool TryMoveEntityBy(int entityId, int dx, int dy)
        {
            if (!_entities.TryGetValue(entityId, out Entity? entity) || entity == null)
                return false;

            entity.X += dx;
            entity.Y += dy;

            return true;
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
            LifecycleStep();

            // Put deterministic per-tick world logic here (regen, ai, projectiles, etc.)
        }

        public FlowSnapshot BuildFlowSnapshot()
        {
            // Keep it compact and always safe to read on client.
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
