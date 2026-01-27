using System.Collections.Generic;
using Net;

namespace Client2.Game
{
    /// <summary>
    /// ClientModel = lightweight rebuildable state for rendering/UI.
    /// Baseline seeds entities. Sample lane updates positions.
    /// Reliable lane updates flow.
    /// </summary>
    public sealed class ClientModel
    {
        public int LastServerTick { get; internal set; }
        public uint LastStateHash { get; internal set; }

        private readonly Dictionary<int, EntityState> _entities = new Dictionary<int, EntityState>();

        public IReadOnlyDictionary<int, EntityState> Entities => _entities;

        public readonly FlowView Flow = new FlowView();

        public void ResetFromBaseline(BaselineMsg baseline)
        {
            _entities.Clear();
            for (int i = 0; i < baseline.Entities.Length; i++)
            {
                EntityState e = baseline.Entities[i];
                _entities[e.Id] = e;
            }

            LastServerTick = baseline.ServerTick;
            LastStateHash = baseline.StateHash;
        }

        public void ApplyPositionAt(int id, int x, int y)
        {
            if (_entities.TryGetValue(id, out EntityState existing))
                _entities[id] = new EntityState(id, x, y, existing.Hp);
            else
                _entities[id] = new EntityState(id, x, y, 0);
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

            internal void ApplyFlowSnapshot(
                byte flowState,
                byte roundState,
                byte lastMetTarget,
                byte cookAttemptsUsed,
                int levelIndex,
                int roundIndex,
                int selectedChefHatId,
                int targetScore,
                int cumulativeScore,
                int cookResultSeq,
                int lastCookScoreDelta)
            {
                FlowState = flowState;
                RoundState = roundState;
                LastCookMetTarget = lastMetTarget != 0;
                CookAttemptsUsed = cookAttemptsUsed;

                LevelIndex = levelIndex;
                RoundIndex = roundIndex;
                SelectedChefHatId = selectedChefHatId;

                TargetScore = targetScore;
                CumulativeScore = cumulativeScore;
                CookResultSeq = cookResultSeq;
                LastCookScoreDelta = lastCookScoreDelta;
            }
        }
    }
}
