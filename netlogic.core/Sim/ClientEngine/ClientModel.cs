using System.Collections.Generic;
using com.aqua.netlogic.net;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine
{
    /// <summary>
    /// ClientModel = lightweight rebuildable state for rendering/UI.
    /// Seeds from a full snapshot, then applies RepOps (positions/lifecycle/flow).
    /// </summary>
    public sealed class ClientModel
    {
        public int LastServerTick { get; internal set; }
        public uint LastStateHash { get; internal set; }

        private readonly Dictionary<int, EntityState> _entities = new Dictionary<int, EntityState>();
        public IReadOnlyDictionary<int, EntityState> Entities => _entities;

        public readonly FlowView Flow = new FlowView();

        public void ResetFromSnapshot(GameSnapshot snap, int serverTick, uint stateHash)
        {
            _entities.Clear();

            SampleEntityPos[] ents = snap.Entities;
            for (int i = 0; i < ents.Length; i++)
            {
                SampleEntityPos e = ents[i];
                _entities[e.EntityId] = new EntityState(e.EntityId, e.X, e.Y, e.Hp);
            }

            Flow.ApplyFlowSnapshot(snap.Flow);

            LastServerTick = serverTick;
            LastStateHash = stateHash;
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
        }

        public sealed class FlowView
        {
            public byte FlowState { get; internal set; }
            public byte RoundState { get; internal set; }
            public bool LastCookMetTarget { get; internal set; }
            public int CookAttemptsUsed { get; internal set; }

            public int LevelIndex { get; internal set; }
            public int RoundIndex { get; internal set; }
            public int SelectedChefHatId { get; internal set; }

            public int TargetScore { get; internal set; }
            public int CumulativeScore { get; internal set; }
            public int CookResultSeq { get; internal set; }
            public int LastCookScoreDelta { get; internal set; }

            internal void ApplyFlowSnapshot(FlowSnapshot flow)
            {
                FlowState = (byte)flow.FlowState;
                RoundState = (byte)flow.RoundState;
                LastCookMetTarget = flow.LastCookMetTarget;
                CookAttemptsUsed = flow.CookAttemptsUsed;

                LevelIndex = flow.LevelIndex;
                RoundIndex = flow.RoundIndex;
                SelectedChefHatId = flow.SelectedChefHatId;

                TargetScore = flow.TargetScore;
                CumulativeScore = flow.CumulativeScore;
                CookResultSeq = flow.CookResultSeq;
                LastCookScoreDelta = flow.LastCookScoreDelta;
            }
        }
    }
}
