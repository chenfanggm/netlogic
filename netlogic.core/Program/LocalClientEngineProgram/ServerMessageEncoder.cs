using LiteNetLib.Utils;
using com.aqua.netlogic.net;
using com.aqua.netlogic.net.wirestate;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.snapshot;

namespace com.aqua.netlogic.program
{
    /// <summary>
    /// Converts ServerEngine outputs into messages that ClientEngine can consume,
    /// WITHOUT any transport/reliability layer.
    /// </summary>
    internal sealed class ServerMessageEncoder
    {
        private readonly NetDataWriter _w = new NetDataWriter();

        private uint _reliableSeq;

        public ServerMessageEncoder()
        {
            _reliableSeq = 0;
        }

        public static BaselineMsg BuildBaseline(int serverTick, Game world)
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
