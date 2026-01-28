using System;
using LiteNetLib.Utils;
using com.aqua.netlogic.net;
using com.aqua.netlogic.net.wirestate;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.protocol
{
    /// <summary>
    /// Decodes wire messages into client-facing data:
    /// - BaselineMsg  -> GameSnapshot (full state)
    /// - ServerOpsMsg -> ReplicationUpdate (RepOp[])
    ///
    /// Owns protocol/hash contract validation for the client-side pipeline.
    /// </summary>
    public static class ClientMessageDecoder
    {
        public static GameSnapshot DecodeBaselineToSnapshot(BaselineMsg baseline, out int serverTick, out uint stateHash)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));

            if (baseline.ProtocolVersion != ProtocolVersion.Current)
                throw new InvalidOperationException(
                    $"Protocol mismatch. Client={ProtocolVersion.Current} Server={baseline.ProtocolVersion}");

            if (baseline.HashScopeId != HashContract.ScopeId || baseline.HashPhase != (byte)HashContract.Phase)
                throw new InvalidOperationException(
                    $"Hash contract mismatch on Baseline. Client scope/phase={HashContract.ScopeId}/{(byte)HashContract.Phase} " +
                    $"Server scope/phase={baseline.HashScopeId}/{baseline.HashPhase}");

            // WireFlowState -> FlowSnapshot
            WireFlowState wf = baseline.Flow;
            FlowSnapshot flow = new FlowSnapshot(
                (com.aqua.netlogic.sim.game.flow.GameFlowState)wf.FlowState,
                wf.LevelIndex,
                wf.RoundIndex,
                wf.SelectedChefHatId,
                wf.TargetScore,
                wf.CumulativeScore,
                wf.CookAttemptsUsed,
                (com.aqua.netlogic.sim.game.flow.RoundState)wf.RoundState,
                wf.CookResultSeq,
                wf.LastCookScoreDelta,
                wf.LastCookMetTarget);

            // WireEntityState[] -> SampleEntityPos[]
            WireEntityState[] ents = baseline.Entities;
            SampleEntityPos[] sample = new SampleEntityPos[ents.Length];
            for (int i = 0; i < ents.Length; i++)
            {
                WireEntityState e = ents[i];
                sample[i] = new SampleEntityPos(e.Id, e.X, e.Y, e.Hp);
            }

            serverTick = baseline.ServerTick;
            stateHash = baseline.StateHash;
            return new GameSnapshot(flow, sample);
        }

        public static ReplicationUpdate DecodeServerOpsToUpdate(ServerOpsMsg msg, bool isReliableLane)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            if (msg.ProtocolVersion != ProtocolVersion.Current)
                throw new InvalidOperationException(
                    $"Protocol mismatch. Client={ProtocolVersion.Current} Server={msg.ProtocolVersion}");

            if (msg.HashScopeId != HashContract.ScopeId || msg.HashPhase != (byte)HashContract.Phase)
                throw new InvalidOperationException(
                    $"Hash contract mismatch on ServerOps. Client scope/phase={HashContract.ScopeId}/{(byte)HashContract.Phase} " +
                    $"Server scope/phase={msg.HashScopeId}/{msg.HashPhase}");

            // Heartbeat is meaningful: advances tick/hash even with zero ops
            if (msg.OpCount == 0 || msg.OpsPayload == null || msg.OpsPayload.Length == 0)
            {
                return new ReplicationUpdate(
                    serverTick: msg.ServerTick,
                    serverSeq: msg.ServerSeq,
                    stateHash: msg.StateHash,
                    isReliable: isReliableLane,
                    ops: Array.Empty<RepOp>());
            }

            NetDataReader r = new NetDataReader(msg.OpsPayload);

            RepOp[] ops = new RepOp[msg.OpCount];
            int outIdx = 0;

            ushort remaining = msg.OpCount;
            while (remaining > 0)
            {
                remaining--;

                OpType opType = OpsReader.ReadOpType(r);
                ushort opLen = OpsReader.ReadOpLen(r);

                int startPos = r.Position;

                switch (opType)
                {
                    case OpType.PositionSnapshot:
                    {
                        int id = r.GetInt();
                        int x = r.GetInt();
                        int y = r.GetInt();
                        ops[outIdx++] = RepOp.PositionSnapshot(id, x, y);
                        break;
                    }

                    case OpType.EntitySpawned:
                    {
                        int id = r.GetInt();
                        int x = r.GetInt();
                        int y = r.GetInt();
                        int hp = r.GetInt();
                        ops[outIdx++] = RepOp.EntitySpawned(id, x, y, hp);
                        break;
                    }

                    case OpType.EntityDestroyed:
                    {
                        int id = r.GetInt();
                        ops[outIdx++] = RepOp.EntityDestroyed(id);
                        break;
                    }

                    case OpType.FlowFire:
                    {
                        // Payload layout matches OpsWriter.WriteFlowFire
                        byte trigger = r.GetByte();
                        r.GetByte(); r.GetByte(); r.GetByte(); // padding
                        int param0 = r.GetInt();
                        ops[outIdx++] = RepOp.FlowFire(trigger, param0);
                        break;
                    }

                    case OpType.FlowSnapshot:
                    {
                        // Payload layout matches OpsWriter.WriteFlowSnapshot
                        byte flowState = r.GetByte();
                        byte roundState = r.GetByte();
                        byte lastMetTarget = r.GetByte();
                        byte cookAttemptsUsed = r.GetByte();

                        int levelIndex = r.GetInt();
                        int roundIndex = r.GetInt();
                        int selectedChefHatId = r.GetInt();
                        int targetScore = r.GetInt();
                        int cumulativeScore = r.GetInt();
                        int cookResultSeq = r.GetInt();
                        int lastCookScoreDelta = r.GetInt();

                        ops[outIdx++] = RepOp.FlowSnapshot(
                            flowState: flowState,
                            roundState: roundState,
                            lastCookMetTarget: lastMetTarget,
                            cookAttemptsUsed: cookAttemptsUsed,
                            levelIndex: levelIndex,
                            roundIndex: roundIndex,
                            selectedChefHatId: selectedChefHatId,
                            targetScore: targetScore,
                            cumulativeScore: cumulativeScore,
                            cookResultSeq: cookResultSeq,
                            lastCookScoreDelta: lastCookScoreDelta);
                        break;
                    }

                    default:
                    {
                        // Unknown op: skip payload, keep stream aligned
                        OpsReader.SkipBytes(r, opLen);
                        ops[outIdx++] = new RepOp(RepOpType.None);
                        break;
                    }
                }

                // enforce opLen alignment
                int consumed = r.Position - startPos;
                if (consumed < opLen)
                    OpsReader.SkipBytes(r, opLen - consumed);
                else if (consumed > opLen)
                    throw new InvalidOperationException(
                        $"Ops decode overread: opType={opType} expectedLen={opLen} consumed={consumed}");
            }

            // If any unknown ops produced RepOpType.None, keep them. ClientEngine can ignore them.

            return new ReplicationUpdate(
                serverTick: msg.ServerTick,
                serverSeq: msg.ServerSeq,
                stateHash: msg.StateHash,
                isReliable: isReliableLane,
                ops: ops);
        }
    }
}
