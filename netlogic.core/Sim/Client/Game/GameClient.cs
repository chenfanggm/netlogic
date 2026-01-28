using System;
using Client.Net;
using Client.Protocol;
using LiteNetLib.Utils;
using Net;
using Sim.Game.Flow;
using Sim.Snapshot;

namespace Client.Game
{
    /// <summary>
    /// GameClient = applies decoded messages into ClientModel.
    /// Rendering reads Model.
    ///
    /// Supports 3 input shapes:
    /// 1) ServerSnapshot (legacy object transport harness)
    /// 2) BaselineMsg (baseline seed)
    /// 3) ServerOpsMsg (reliable/unreliable ops stream)
    /// </summary>
    public sealed class GameClient
    {
        public ClientModel Model { get; } = new ClientModel();

        private readonly NetworkClient? _net;

        public GameClient()
        {
            _net = null;
        }

        public GameClient(NetworkClient net)
        {
            _net = net ?? throw new ArgumentNullException(nameof(net));
            _net.OnServerSnapshot += OnServerSnapshot;
        }

        public void Poll() => _net?.Poll();

        // -------------------------
        // Input sending API (example)
        // -------------------------

        public void SendMoveBy(int entityId, int dx, int dy)
        {
            if (_net == null)
                throw new InvalidOperationException("GameClient has no NetworkClient. Use direct ApplyBaseline/ApplyServerOps in local harness.");

            ClientCommand cmd = ClientCommand.MoveBy(entityId, dx, dy);
            _net.SendClientCommand(cmd);
        }

        public void SendFlowFire(byte trigger, int param0)
        {
            if (_net == null)
                throw new InvalidOperationException("GameClient has no NetworkClient. Use direct ApplyBaseline/ApplyServerOps in local harness.");

            ClientCommand cmd = ClientCommand.FlowFire(trigger, param0);
            _net.SendClientCommand(cmd);
        }

        // -------------------------
        // Receive/apply (direct, transport-neutral)
        // -------------------------

        public void ApplyBaseline(BaselineMsg baseline)
        {
            Model.ResetFromBaseline(baseline);
        }

        public void ApplyServerOps(ServerOpsMsg msg)
        {
            if (msg.ProtocolVersion != ProtocolVersion.Current)
                throw new InvalidOperationException(
                    $"Protocol mismatch. Client={ProtocolVersion.Current} Server={msg.ProtocolVersion}");

            if (msg.HashScopeId != HashContract.ScopeId || msg.HashPhase != (byte)HashContract.Phase)
                throw new InvalidOperationException(
                    $"Hash contract mismatch on ServerOps. Client scope/phase={HashContract.ScopeId}/{(byte)HashContract.Phase} " +
                    $"Server scope/phase={msg.HashScopeId}/{msg.HashPhase}");

            // Empty message still advances tick/hash.
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
                        // Payload (32 bytes):
                        // [byte flowState][byte roundState][byte lastMetTarget][byte attemptsUsed]
                        // [int levelIndex][int roundIndex][int selectedHatId]
                        // [int targetScore][int cumulativeScore]
                        // [int cookResultSeq][int lastCookScoreDelta]
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
                            flowState: (GameFlowState)flowState,
                            levelIndex: levelIndex,
                            roundIndex: roundIndex,
                            selectedChefHatId: selectedChefHatId,
                            targetScore: targetScore,
                            cumulativeScore: cumulativeScore,
                            cookAttemptsUsed: cookAttemptsUsed,
                            roundState: (RoundState)roundState,
                            cookResultSeq: cookResultSeq,
                            lastCookScoreDelta: lastCookScoreDelta,
                            lastCookMetTarget: lastMetTarget != 0);

                        Model.Flow.ApplyFlowSnapshot(flow);
                        break;
                    }

                    default:
                    {
                        // Unknown op: skip to preserve stream alignment.
                        OpsReader.SkipBytes(r, opLen);
                        break;
                    }
                }

                // Ensure we consume exactly opLen bytes (skip remainder if handler read less).
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

        // -------------------------
        // Legacy snapshot path
        // -------------------------

        private void OnServerSnapshot(ServerSnapshot snapshot)
        {
            Model.ApplySnapshot(snapshot);
        }
    }
}
