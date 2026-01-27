using System.Collections.Generic;
using Client2.Protocol;
using Net;
using Net.WireState;
using Sim.Snapshot;

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
            if (baseline.ProtocolVersion != ProtocolVersion.Current)
                throw new InvalidOperationException(
                    $"Protocol mismatch. Client={ProtocolVersion.Current} Server={baseline.ProtocolVersion}");

            _entities.Clear();
            for (int i = 0; i < baseline.Entities.Length; i++)
            {
                WireEntityState e = baseline.Entities[i];
                _entities[e.Id] = new EntityState(e.Id, e.X, e.Y, e.Hp);
            }

            FlowSnapshot flow = new FlowSnapshot(
                (Sim.Game.Flow.GameFlowState)baseline.Flow.FlowState,
                baseline.Flow.LevelIndex,
                baseline.Flow.RoundIndex,
                baseline.Flow.SelectedChefHatId,
                baseline.Flow.TargetScore,
                baseline.Flow.CumulativeScore,
                baseline.Flow.CookAttemptsUsed,
                (Sim.Game.Flow.RoundState)baseline.Flow.RoundState,
                baseline.Flow.CookResultSeq,
                baseline.Flow.LastCookScoreDelta,
                baseline.Flow.LastCookMetTarget);
            Flow.ApplyFlowSnapshot(flow);

            LastServerTick = baseline.ServerTick;
            LastStateHash = baseline.StateHash;
        }

        public void ApplySnapshot(ServerSnapshot snapshot)
        {
            _entities.Clear();
            for (int i = 0; i < snapshot.Entities.Length; i++)
            {
                EntityState e = snapshot.Entities[i];
                _entities[e.Id] = e;
            }

            Flow.ApplyFlowSnapshot(snapshot.Flow);

            LastServerTick = snapshot.ServerTick;
            LastStateHash = snapshot.StateHash;
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
