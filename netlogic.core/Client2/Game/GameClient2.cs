using System;
using Client2.Net;
using LiteNetLib.Utils;
using Net;

namespace Client2.Game
{
    /// <summary>
    /// GameClient2 = applies decoded messages into ClientModel.
    /// Rendering reads Model.
    /// </summary>
    public sealed class GameClient2
    {
        public ClientModel Model { get; } = new ClientModel();

        private readonly NetworkClient2? _net;

        private int _lastAppliedSampleTick;
        private uint _lastAppliedReliableSeq;

        private readonly NetDataWriter _clientOpsWriter;

        public GameClient2(NetworkClient2 net)
        {
            _net = net ?? throw new ArgumentNullException(nameof(net));

            _net.BaselineReceived += OnBaseline;
            _net.ServerOpsReceived += OnServerOps;

            _lastAppliedSampleTick = -1;
            _lastAppliedReliableSeq = 0;

            _clientOpsWriter = new NetDataWriter();
        }

        public GameClient2(Client2.Net.InProcessNetFeed feed)
        {
            if (feed == null) throw new ArgumentNullException(nameof(feed));

            feed.BaselineReceived += OnBaseline;
            feed.ServerOpsReceived += OnServerOps;

            _lastAppliedSampleTick = -1;
            _lastAppliedReliableSeq = 0;

            _clientOpsWriter = new NetDataWriter();
        }

        public void Start() => EnsureNet().Start();
        public void Connect(string host, int port) => EnsureNet().Connect(host, port);
        public void Poll() => EnsureNet().Poll();

        // -------------------------
        // Input sending API (example)
        // -------------------------

        public void SendMoveBy(int targetServerTick, uint clientCmdSeq, int entityId, int dx, int dy)
        {
            _clientOpsWriter.Reset();
            ushort opCount = 0;

            OpsWriter.WriteMoveBy(_clientOpsWriter, entityId, dx, dy);
            opCount++;

            byte[] opsBytes = _clientOpsWriter.CopyData();

            ClientOpsMsg msg = new ClientOpsMsg(
                clientTick: targetServerTick,
                clientCmdSeq: clientCmdSeq,
                opCount: opCount,
                opsPayload: opsBytes);

            byte[] packet = MsgCodec.EncodeClientOps(msg);
            EnsureNet().Send(Lane.Reliable, new ArraySegment<byte>(packet, 0, packet.Length));
        }

        public void SendFlowFire(int targetServerTick, uint clientCmdSeq, byte trigger, int param0)
        {
            _clientOpsWriter.Reset();
            ushort opCount = 0;

            OpsWriter.WriteFlowFire(_clientOpsWriter, trigger, param0);
            opCount++;

            byte[] opsBytes = _clientOpsWriter.CopyData();

            ClientOpsMsg msg = new ClientOpsMsg(
                clientTick: targetServerTick,
                clientCmdSeq: clientCmdSeq,
                opCount: opCount,
                opsPayload: opsBytes);

            byte[] packet = MsgCodec.EncodeClientOps(msg);
            EnsureNet().Send(Lane.Reliable, new ArraySegment<byte>(packet, 0, packet.Length));
        }

        // -------------------------
        // Receive/apply
        // -------------------------

        private void OnBaseline(BaselineMsg baseline)
        {
            Model.ResetFromBaseline(baseline);
            _lastAppliedSampleTick = baseline.ServerTick;
            _lastAppliedReliableSeq = 0;
        }

        private void OnServerOps(ServerOpsMsg msg, Lane lane)
        {
            if (lane == Lane.Sample)
                ApplySampleOps(msg);
            else
                ApplyReliableOps(msg);
        }

        private void ApplySampleOps(ServerOpsMsg msg)
        {
            if (msg.ServerTick <= _lastAppliedSampleTick)
                return;

            _lastAppliedSampleTick = msg.ServerTick;

            NetDataReader r = new NetDataReader(msg.OpsPayload, 0, msg.OpsPayload.Length);

            int i = 0;
            while (i < msg.OpCount)
            {
                OpType opType = OpsReader.ReadOpType(r);
                ushort opLen = OpsReader.ReadOpLen(r);

                if (opType == OpType.PositionAt)
                {
                    int id = r.GetInt();
                    int x = r.GetInt();
                    int y = r.GetInt();
                    Model.ApplyPositionAt(id, x, y);
                }
                else
                {
                    OpsReader.SkipBytes(r, opLen);
                }

                i++;
            }

            Model.LastServerTick = msg.ServerTick;
            Model.LastStateHash = msg.StateHash;
        }

        private void ApplyReliableOps(ServerOpsMsg msg)
        {
            // Idempotency guard
            if (msg.ServerSeq <= _lastAppliedReliableSeq)
                return;

            _lastAppliedReliableSeq = msg.ServerSeq;

            NetDataReader r = new NetDataReader(msg.OpsPayload, 0, msg.OpsPayload.Length);

            int i = 0;
            while (i < msg.OpCount)
            {
                OpType opType = OpsReader.ReadOpType(r);
                ushort opLen = OpsReader.ReadOpLen(r);

                if (opType == OpType.FlowSnapshot)
                {
                    byte flowState = r.GetByte();
                    byte roundState = r.GetByte();
                    byte lastMetTarget = r.GetByte();
                    byte attemptsUsed = r.GetByte();

                    int levelIndex = r.GetInt();
                    int roundIndex = r.GetInt();
                    int selectedHat = r.GetInt();

                    int targetScore = r.GetInt();
                    int cumulativeScore = r.GetInt();

                    int cookSeq = r.GetInt();
                    int lastDelta = r.GetInt();

                    Model.Flow.ApplyFlowSnapshot(
                        flowState, roundState, lastMetTarget, attemptsUsed,
                        levelIndex, roundIndex, selectedHat,
                        targetScore, cumulativeScore, cookSeq, lastDelta);
                }
                else
                {
                    OpsReader.SkipBytes(r, opLen);
                }

                i++;
            }

            // Ack reliable seq back to server
            ClientAckMsg ack = new ClientAckMsg(_lastAppliedReliableSeq);
            byte[] packet = MsgCodec.EncodeClientAck(ack);
            if (_net != null)
                _net.Send(Lane.Reliable, new ArraySegment<byte>(packet, 0, packet.Length));
        }

        private NetworkClient2 EnsureNet()
        {
            if (_net == null)
                throw new InvalidOperationException("Network client not configured for this GameClient2 instance.");
            return _net;
        }
    }
}
