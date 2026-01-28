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
    /// Decodes wire messages into client-facing primitives.
    ///
    /// Motivation:
    /// - Keep ClientEngine free of wire/protocol parsing.
    /// - Centralize protocol validation (version + hash contract).
    /// - Enable replay/testing by feeding ClientEngine decoded updates.
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

            // Wire -> SampleEntityPos[]
            WireEntityState[] wireEnts = baseline.Entities ?? Array.Empty<WireEntityState>();
            SampleEntityPos[] ents = new SampleEntityPos[wireEnts.Length];
            for (int i = 0; i < wireEnts.Length; i++)
            {
                WireEntityState e = wireEnts[i];
                ents[i] = new SampleEntityPos(e.Id, e.X, e.Y, e.Hp);
            }

            serverTick = baseline.ServerTick;
            stateHash = baseline.StateHash;
            return new GameSnapshot(flow, ents);
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
            ushort remaining = msg.OpCount;

            // RepOps are fixed-width, so we can decode into a tight array.
            // If unknown ops exist, we skip and compact.
            RepOp[] tmp = new RepOp[remaining];
            int outCount = 0;

            while (remaining > 0)
            {
                remaining--;

                OpType opType = OpsReader.ReadOpType(r);
                ushort opLen = OpsReader.ReadOpLen(r);

                int startPos = r.Position;

                bool recognized = true;
                RepOp rep = default;

                switch (opType)
                {
                    case OpType.PositionSnapshot:
                    {
                        int id = r.GetInt();
                        int x = r.GetInt();
                        int y = r.GetInt();
                        rep = RepOp.PositionSnapshot(id, x, y);
                        break;
                    }

                    case OpType.EntitySpawned:
                    {
                        int id = r.GetInt();
                        int x = r.GetInt();
                        int y = r.GetInt();
                        int hp = r.GetInt();
                        rep = RepOp.EntitySpawned(id, x, y, hp);
                        break;
                    }

                    case OpType.EntityDestroyed:
                    {
                        int id = r.GetInt();
                        rep = RepOp.EntityDestroyed(id);
                        break;
                    }

                    case OpType.FlowFire:
                    {
                        // Payload: [byte trigger][3 pad][int param0]
                        byte trigger = r.GetByte();
                        r.GetByte(); r.GetByte(); r.GetByte(); // pad
                        int param0 = r.GetInt();
                        rep = RepOp.FlowFire(trigger, param0);
                        break;
                    }

                    case OpType.FlowSnapshot:
                    {
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

                        rep = RepOp.FlowSnapshot(
                            flowState,
                            roundState,
                            lastMetTarget,
                            cookAttemptsUsed,
                            levelIndex,
                            roundIndex,
                            selectedChefHatId,
                            targetScore,
                            cumulativeScore,
                            cookResultSeq,
                            lastCookScoreDelta);
                        break;
                    }

                    default:
                        recognized = false;
                        break;
                }

                int consumed = r.Position - startPos;
                if (consumed < opLen)
                    OpsReader.SkipBytes(r, opLen - consumed);
                else if (consumed > opLen)
                    throw new InvalidOperationException(
                        $"Ops decode overread: opType={opType} expectedLen={opLen} consumed={consumed}");

                if (recognized)
                    tmp[outCount++] = rep;
            }

            RepOp[] ops;
            if (outCount == tmp.Length)
            {
                ops = tmp;
            }
            else
            {
                ops = new RepOp[outCount];
                Array.Copy(tmp, ops, outCount);
            }

            return new ReplicationUpdate(
                serverTick: msg.ServerTick,
                serverSeq: msg.ServerSeq,
                stateHash: msg.StateHash,
                isReliable: isReliableLane,
                ops: ops);
        }
    }
}
