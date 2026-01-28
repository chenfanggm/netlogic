using LiteNetLib.Utils;
using Net;
using Net.WireState;
using Sim.Engine;
using Sim.Game;
using Sim.Snapshot;

namespace Program
{
    /// <summary>
    /// Converts ServerEngine outputs into messages that ClientEngine can consume,
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
            WireEntityState[] wireEntities = MapWireEntities(entities);
            WireFlowState wireFlow = MapWireFlow(world.BuildFlowSnapshot());

            return new BaselineMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                StateSchema.BaselineSchemaId,
                serverTick,
                StateHash.ComputeWorldHash(world),
                wireFlow,
                wireEntities);
        }

        public BaselineMsg BuildBaselineFromSnapshot(int serverTick, uint stateHash, GameSnapshot snapshot)
        {
            SampleEntityPos[] sample = snapshot.Entities;
            EntityState[] entities = new EntityState[sample.Length];

            int i = 0;
            while (i < sample.Length)
            {
                SampleEntityPos e = sample[i];
                entities[i] = new EntityState(e.EntityId, e.X, e.Y, e.Hp);
                i++;
            }

            WireEntityState[] wireEntities = MapWireEntities(entities);
            WireFlowState wireFlow = MapWireFlow(snapshot.Flow);

            return new BaselineMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                StateSchema.BaselineSchemaId,
                serverTick,
                stateHash,
                wireFlow,
                wireEntities);
        }

        public ServerOpsMsg BuildUnreliableOpsFromSnapshot(int serverTick, Game world)
        {
            _w.Reset();
            ushort opCount = 0;

            foreach (Entity e in world.Entities)
            {
                OpsWriter.WritePositionSnapshot(_w, e.Id, e.X, e.Y);
                opCount++;
            }

            byte[] payload = (opCount == 0) ? Array.Empty<byte>() : _w.CopyData();

            return new ServerOpsMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                serverTick,
                0, // unreliable lane ignores serverSeq
                StateHash.ComputeWorldHash(world),
                opCount,
                payload);
        }

        public ServerOpsMsg BuildUnreliableOpsFromRepOps(int serverTick, uint stateHash, ReadOnlySpan<RepOp> ops)
        {
            _w.Reset();
            ushort opCount = 0;

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];
                if (op.Type == RepOpType.PositionSnapshot)
                {
                    OpsWriter.WritePositionSnapshot(_w, op.A, op.B, op.C);
                    opCount++;
                }
            }

            byte[] payload = (opCount == 0) ? Array.Empty<byte>() : _w.CopyData();

            return new ServerOpsMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                serverTick,
                0, // unreliable lane ignores serverSeq
                stateHash,
                opCount,
                payload);
        }

        public bool TryBuildReliableOpsFromRepOps(int serverTick, uint stateHash, RepOp[] ops, out ServerOpsMsg msg)
        {
            _w.Reset();
            ushort opCount = 0;

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];

                switch (op.Type)
                {
                    case RepOpType.EntitySpawned:
                        OpsWriter.WriteEntitySpawned(_w, op.A, op.B, op.C, op.D);
                        opCount++;
                        break;

                    case RepOpType.EntityDestroyed:
                        OpsWriter.WriteEntityDestroyed(_w, op.A);
                        opCount++;
                        break;

                    case RepOpType.FlowSnapshot:
                        byte flowState = (byte)(op.A & 0xFF);
                        byte roundState = (byte)((op.A >> 8) & 0xFF);
                        byte lastMetTarget = (byte)((op.A >> 16) & 0xFF);
                        byte cookAttemptsUsed = (byte)((op.A >> 24) & 0xFF);

                        OpsWriter.WriteFlowSnapshot(
                            _w,
                            flowState: flowState,
                            roundState: roundState,
                            lastMetTarget: lastMetTarget,
                            cookAttemptsUsed: cookAttemptsUsed,
                            levelIndex: op.B,
                            roundIndex: op.C,
                            selectedChefHatId: op.D,
                            targetScore: op.E,
                            cumulativeScore: op.F,
                            cookResultSeq: op.G,
                            lastCookScoreDelta: op.H);
                        opCount++;
                        break;

                    default:
                        break;
                }
            }

            if (opCount == 0)
            {
                msg = new ServerOpsMsg();
                return false;
            }

            _reliableSeq++;

            byte[] payload = _w.CopyData();

            msg = new ServerOpsMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                serverTick,
                _reliableSeq,
                stateHash,
                opCount,
                payload);

            return true;
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
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                serverTick,
                _reliableSeq,
                StateHash.ComputeWorldHash(world),
                opCount,
                payload);

            return true;
        }

        private static EntityState[] BuildEntityStates(Game world)
        {
            return world.ToSnapshot();
        }

        private static WireEntityState[] MapWireEntities(EntityState[] entities)
        {
            WireEntityState[] wire = new WireEntityState[entities.Length];
            for (int i = 0; i < entities.Length; i++)
            {
                EntityState e = entities[i];
                wire[i] = new WireEntityState(e.Id, e.X, e.Y, e.Hp);
            }

            return wire;
        }

        private static WireFlowState MapWireFlow(FlowSnapshot flow)
        {
            return new WireFlowState(
                (int)flow.FlowState,
                flow.LevelIndex,
                flow.RoundIndex,
                flow.SelectedChefHatId,
                flow.TargetScore,
                flow.CumulativeScore,
                flow.CookAttemptsUsed,
                (int)flow.RoundState,
                flow.CookResultSeq,
                flow.LastCookScoreDelta,
                flow.LastCookMetTarget);
        }
    }
}
