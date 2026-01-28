using System;
using Client.Protocol;
using LiteNetLib.Utils;
using Net;
using Sim.Snapshot;

namespace Client.Game
{
    /// <summary>
    /// ClientEngine = pure client-side state reconstruction core.
    /// Owns ClientModel, consumes Baseline + Ops.
    ///
    /// No transport, no reliability, no realtime concerns.
    /// </summary>
    public sealed class ClientEngine
    {
        public ClientModel Model { get; } = new ClientModel();

        public void ApplyBaseline(BaselineMsg baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));

            // Hash contract guard (keeps baseline/ops consistent across builds)
            if (baseline.HashScopeId != HashContract.ScopeId || baseline.HashPhase != (byte)HashContract.Phase)
                throw new InvalidOperationException(
                    $"Hash contract mismatch on Baseline. Client scope/phase={HashContract.ScopeId}/{(byte)HashContract.Phase} " +
                    $"Server scope/phase={baseline.HashScopeId}/{baseline.HashPhase}");

            Model.ResetFromBaseline(baseline);
        }

        /// <summary>
        /// Apply a ServerOpsMsg payload (reliable or unreliable lane).
        /// Does NOT handle ack/replay ordering — higher layers do that.
        /// </summary>
        public void ApplyServerOps(ServerOpsMsg msg)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            if (msg.ProtocolVersion != ProtocolVersion.Current)
                throw new InvalidOperationException(
                    $"Protocol mismatch. Client={ProtocolVersion.Current} Server={msg.ProtocolVersion}");

            if (msg.HashScopeId != HashContract.ScopeId || msg.HashPhase != (byte)HashContract.Phase)
                throw new InvalidOperationException(
                    $"Hash contract mismatch on ServerOps. Client scope/phase={HashContract.ScopeId}/{(byte)HashContract.Phase} " +
                    $"Server scope/phase={msg.HashScopeId}/{msg.HashPhase}");

            // Empty message still advances tick/hash
            if (msg.OpCount == 0 || msg.OpsPayload == null || msg.OpsPayload.Length == 0)
            {
                Model.LastServerTick = msg.ServerTick;
                Model.LastStateHash = msg.StateHash;
                return;
            }

            NetDataReader r = new NetDataReader(msg.OpsPayload);

            ushort remaining = msg.OpCount;
            while (remaining > 0)
            {
                remaining--;

                global::Net.OpType opType = OpsReader.ReadOpType(r);
                ushort opLen = OpsReader.ReadOpLen(r);

                int startPos = r.Position;

                switch (opType)
                {
                    case global::Net.OpType.PositionSnapshot:
                    {
                        int id = r.GetInt();
                        int x = r.GetInt();
                        int y = r.GetInt();
                        Model.ApplyPositionSnapshot(id, x, y);
                        break;
                    }

                    case global::Net.OpType.EntitySpawned:
                    {
                        int id = r.GetInt();
                        int x = r.GetInt();
                        int y = r.GetInt();
                        int hp = r.GetInt();
                        Model.ApplyEntitySpawned(id, x, y, hp);
                        break;
                    }

                    case global::Net.OpType.EntityDestroyed:
                    {
                        int id = r.GetInt();
                        Model.ApplyEntityDestroyed(id);
                        break;
                    }

                    case global::Net.OpType.FlowSnapshot:
                    {
                        // Matches OpsWriter.WriteFlowSnapshot payload layout
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

                        FlowSnapshot flow = new FlowSnapshot(
                            (Sim.Game.Flow.GameFlowState)flowState,
                            levelIndex,
                            roundIndex,
                            selectedChefHatId,
                            targetScore,
                            cumulativeScore,
                            cookAttemptsUsed,
                            (Sim.Game.Flow.RoundState)roundState,
                            cookResultSeq,
                            lastCookScoreDelta,
                            lastMetTarget != 0);

                        Model.Flow.ApplyFlowSnapshot(flow);
                        break;
                    }

                    default:
                    {
                        // Unknown op: skip payload to keep stream aligned
                        OpsReader.SkipBytes(r, opLen);
                        break;
                    }
                }

                // Safety: enforce exact opLen consumption
                int consumed = r.Position - startPos;
                if (consumed < opLen)
                    OpsReader.SkipBytes(r, opLen - consumed);
                else if (consumed > opLen)
                    throw new InvalidOperationException(
                        $"Ops decode overread: opType={opType} expectedLen={opLen} consumed={consumed}");
            }

            Model.LastServerTick = msg.ServerTick;
            Model.LastStateHash = msg.StateHash;
        }

    }
}
