using System;
using LiteNetLib.Utils;
using Net;
using Sim.Snapshot;

namespace Client.Game
{
    /// <summary>
    /// DirectGameClient = applies BaselineMsg + ServerOpsMsg directly into ClientModel.
    /// No transport, no codec framing, no reliability/replay.
    ///
    /// This is for in-process harnesses and unit tests that want to validate:
    /// - baseline correctness
    /// - ops correctness (reliable + unreliable lanes)
    /// - client model reconstruction
    /// </summary>
    public sealed class DirectGameClient
    {
        public ClientModel Model { get; } = new ClientModel();

        public void ApplyBaseline(BaselineMsg baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            Model.ResetFromBaseline(baseline);
        }

        /// <summary>
        /// Apply a ServerOpsMsg payload (either reliable or unreliable lane).
        /// Reliable ordering/ack/replay is intentionally NOT handled here.
        /// </summary>
        public void ApplyServerOps(ServerOpsMsg msg)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            // Basic protocol gate (Baseline does the heavy check; ops should match too)
            if (msg.ProtocolVersion != ProtocolVersion.Current)
                throw new InvalidOperationException(
                    $"Protocol mismatch. Client={ProtocolVersion.Current} Server={msg.ProtocolVersion}");

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

                    case (global::Net.OpType)10: // EntitySpawned
                    {
                        int id = r.GetInt();
                        int x = r.GetInt();
                        int y = r.GetInt();
                        int hp = r.GetInt();
                        Model.ApplyEntitySpawned(id, x, y, hp);
                        break;
                    }

                    case (global::Net.OpType)11: // EntityDestroyed
                    {
                        int id = r.GetInt();
                        Model.ApplyEntityDestroyed(id);
                        break;
                    }

                    case global::Net.OpType.FlowSnapshot:
                    {
                        // 4 bytes
                        byte flowState = r.GetByte();
                        byte roundState = r.GetByte();
                        byte lastMetTarget = r.GetByte();
                        byte cookAttemptsUsed = r.GetByte();

                        // 7 ints
                        int levelIndex = r.GetInt();
                        int roundIndex = r.GetInt();
                        int selectedChefHatId = r.GetInt();
                        int targetScore = r.GetInt();
                        int cumulativeScore = r.GetInt();
                        int cookResultSeq = r.GetInt();
                        int lastCookScoreDelta = r.GetInt();

                        FlowSnapshot flow = new FlowSnapshot(
                            flowState: (Sim.Game.Flow.GameFlowState)flowState,
                            levelIndex: levelIndex,
                            roundIndex: roundIndex,
                            selectedChefHatId: selectedChefHatId,
                            targetScore: targetScore,
                            cumulativeScore: cumulativeScore,
                            cookAttemptsUsed: cookAttemptsUsed,
                            roundState: (Sim.Game.Flow.RoundState)roundState,
                            cookResultSeq: cookResultSeq,
                            lastCookScoreDelta: lastCookScoreDelta,
                            lastCookMetTarget: lastMetTarget != 0);

                        Model.Flow.ApplyFlowSnapshot(flow);
                        break;
                    }

                    default:
                    {
                        // Unknown op: skip payload to preserve stream alignment.
                        OpsReader.SkipBytes(r, opLen);
                        break;
                    }
                }

                // Safety: ensure we advanced exactly opLen bytes (or skip remainder).
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
