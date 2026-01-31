using System;
using System.Collections.Generic;
using com.aqua.netlogic.net;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.game.runtime;
using com.aqua.netlogic.sim.game.rules;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine
{
    /// <summary>
    /// ClientModel = lightweight rebuildable state for rendering/UI.
    /// Seeds from a full snapshot, then applies RepOps (positions/lifecycle/flow).
    /// </summary>
    public sealed class ClientModel : IAuthoritativeOpTarget
    {
        public int LastServerTick { get; internal set; }
        public uint LastStateHash { get; internal set; }
        public double NowMs { get; internal set; }

        private readonly Dictionary<int, EntityState> _entities = new Dictionary<int, EntityState>();
        public IReadOnlyDictionary<int, EntityState> Entities => _entities;

        public GameFlowState FlowState { get; set; } = GameFlowState.Boot;

        public RunRuntime Run { get; } = new RunRuntime();
        public LevelRuntime Level { get; } = new LevelRuntime();
        public RoundRuntime Round { get; } = new RoundRuntime();

        private readonly Dictionary<int, int> _hasteTicksRemaining = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _dashCooldownTicksRemaining = new Dictionary<int, int>();

        public void ResetFromSnapshot(ServerModelSnapshot snap)
        {
            if (snap == null) throw new ArgumentNullException(nameof(snap));

            _entities.Clear();
            _hasteTicksRemaining.Clear();
            _dashCooldownTicksRemaining.Clear();

            SampleEntityPos[] ents = snap.Entities;
            for (int i = 0; i < ents.Length; i++)
            {
                SampleEntityPos e = ents[i];
                _entities[e.EntityId] = new EntityState(e.EntityId, e.X, e.Y, e.Hp);
            }

            FlowSnapshot flow = snap.Flow;
            FlowState = flow.FlowState;
            Run.SelectedChefHatId = flow.SelectedChefHatId;
            Run.LevelIndex = flow.LevelIndex;

            Round.RoundIndex = flow.RoundIndex;
            Round.TargetScore = flow.TargetScore;
            Round.CumulativeScore = flow.CumulativeScore;
            Round.CookAttemptsUsed = flow.CookAttemptsUsed;
            Round.State = flow.RoundState;
            Round.LastCookSeq = flow.CookResultSeq;
            Round.LastCookScoreDelta = flow.LastCookScoreDelta;
            Round.LastCookMetTarget = flow.LastCookMetTarget;

            LastServerTick = snap.ServerTick;
            LastStateHash = snap.StateHash;
        }

        public void ApplyPositionSnapshot(int id, int x, int y)
        {
            if (_entities.TryGetValue(id, out EntityState existing))
                _entities[id] = new EntityState(id, x, y, existing.Hp);
            else
                _entities[id] = new EntityState(id, x, y, 0);
        }

        public void ApplyEntitySpawned(int id, int x, int y, int hp)
        {
            _entities[id] = new EntityState(id, x, y, hp);
        }

        public void ApplyEntityDestroyed(int id)
        {
            _entities.Remove(id);
            _hasteTicksRemaining.Remove(id);
            _dashCooldownTicksRemaining.Remove(id);
        }

        public void ApplyEntityBuffSet(int entityId, BuffType buff, int remainingTicks)
        {
            if (buff != BuffType.Haste)
                return;

            if (remainingTicks <= 0)
                _hasteTicksRemaining.Remove(entityId);
            else
                _hasteTicksRemaining[entityId] = remainingTicks;
        }

        public void ApplyEntityCooldownSet(int entityId, CooldownType cd, int remainingTicks)
        {
            if (cd != CooldownType.Dash)
                return;

            if (remainingTicks <= 0)
                _dashCooldownTicksRemaining.Remove(entityId);
            else
                _dashCooldownTicksRemaining[entityId] = remainingTicks;
        }

    }
}
