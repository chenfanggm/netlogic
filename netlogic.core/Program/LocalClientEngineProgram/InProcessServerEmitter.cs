using LiteNetLib.Utils;
using Net;
using Sim.Game;
using Sim.Snapshot;

namespace Program
{
    /// <summary>
    /// Converts GameEngine outputs into messages that GameClient2 can consume,
    /// WITHOUT any transport/reliability layer.
    /// </summary>
    internal sealed class InProcessServerEmitter
    {
        private readonly NetDataWriter _w = new NetDataWriter();

        private FlowSnapshot _lastFlow;
        private bool _hasLastFlow;

        private uint _reliableSeq;

        public InProcessServerEmitter()
        {
            _lastFlow = default;
            _hasLastFlow = false;
            _reliableSeq = 0;
        }

        public BaselineMsg BuildBaseline(int serverTick, Game world)
        {
            EntityState[] entities = BuildEntityStates(world);

            return new BaselineMsg(
                serverTick: serverTick,
                stateHash: StateHash.ComputeWorldHash(world),
                entities: entities);
        }

        public ServerOpsMsg BuildSampleOpsFromSnapshot(int serverTick, Game world)
        {
            _w.Reset();
            ushort opCount = 0;

            foreach (Entity e in world.Entities)
            {
                OpsWriter.WritePositionAt(_w, e.Id, e.X, e.Y);
                opCount++;
            }

            byte[] payload = (opCount == 0) ? Array.Empty<byte>() : _w.CopyData();

            return new ServerOpsMsg(
                serverTick: serverTick,
                stateHash: StateHash.ComputeWorldHash(world),
                serverSeq: 0, // sample lane ignores serverSeq
                opCount: opCount,
                opsPayload: payload);
        }

        public bool TryBuildReliableFlowSnapshot(int serverTick, Game world, out ServerOpsMsg msg)
        {
            // Only emit if flow changes
            FlowSnapshot flow = world.Snapshot().Flow;

            if (_hasLastFlow
                && _lastFlow.FlowState == flow.FlowState
                && _lastFlow.LevelIndex == flow.LevelIndex
                && _lastFlow.RoundIndex == flow.RoundIndex
                && _lastFlow.SelectedChefHatId == flow.SelectedChefHatId
                && _lastFlow.TargetScore == flow.TargetScore
                && _lastFlow.CumulativeScore == flow.CumulativeScore
                && _lastFlow.CookAttemptsUsed == flow.CookAttemptsUsed
                && _lastFlow.RoundState == flow.RoundState
                && _lastFlow.CookResultSeq == flow.CookResultSeq
                && _lastFlow.LastCookScoreDelta == flow.LastCookScoreDelta
                && _lastFlow.LastCookMetTarget == flow.LastCookMetTarget)
            {
                msg = new ServerOpsMsg();
                return false;
            }

            _hasLastFlow = true;
            _lastFlow = flow;

            _reliableSeq++;

            _w.Reset();
            ushort opCount = 0;

            OpsWriter.WriteFlowSnapshot(
                _w,
                flowState: (byte)flow.FlowState,
                roundState: (byte)flow.RoundState,
                lastMetTarget: (byte)(flow.LastCookMetTarget ? 1 : 0),
                cookAttemptsUsed: (byte)flow.CookAttemptsUsed,
                levelIndex: flow.LevelIndex,
                roundIndex: flow.RoundIndex,
                selectedChefHatId: flow.SelectedChefHatId,
                targetScore: flow.TargetScore,
                cumulativeScore: flow.CumulativeScore,
                cookResultSeq: flow.CookResultSeq,
                lastCookScoreDelta: flow.LastCookScoreDelta);

            opCount++;

            byte[] payload = _w.CopyData();

            msg = new ServerOpsMsg(
                serverTick: serverTick,
                stateHash: StateHash.ComputeWorldHash(world),
                serverSeq: _reliableSeq,
                opCount: opCount,
                opsPayload: payload);

            return true;
        }

        private static EntityState[] BuildEntityStates(Game world)
        {
            return world.ToSnapshot();
        }
    }
}
