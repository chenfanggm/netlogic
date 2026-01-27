using Sim.Game;
using Sim.Engine;
using LiteNetLib.Utils;
using Net;
using Client2.Protocol;
using Sim.Server.Reliability;
using Sim.Command;
using Sim.Time;

namespace Sim.Server
{
    /// <summary>
    /// Thin network adapter:
    /// - Owns transport
    /// - Owns handshake/ping/ack/baseline
    /// - Converts wire ClientOps -> ClientCommand[]
    /// - Converts ClientCommand[] -> EngineCommand[]
    /// - Feeds ServerEngine
    /// - Calls TickOnce()
    /// - Hashes + encodes TickFrame into wire packets (Reliable + Sample)
    /// </summary>
    public sealed class GameServer
    {
        private readonly IServerTransport _transport;
        private readonly GameEngine _engine;
        private readonly int _tickRateHz;

        private readonly ClientOpsMsgToClientCommandConverter _converter;
        private readonly List<int> _clients;
        private readonly HashSet<int> _clientSet;
        private readonly Dictionary<int, ServerReliableStream> _reliableStreams;

        private const int ReliableMaxOpsBytesPerTick = 8 * 1024;
        private const int ReliableMaxPendingPackets = 128;

        private uint _serverSampleSeq;
        private readonly NetDataWriter _opsWriter;


        public int CurrentServerTick => _engine.CurrentTick;
        public int TickRateHz => _tickRateHz;

        public GameServer(IServerTransport transport, int tickRateHz, Game.Game initialGame)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (tickRateHz <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRateHz));

            _tickRateHz = tickRateHz;
            _engine = new GameEngine(initialGame ?? throw new ArgumentNullException(nameof(initialGame)));

            _converter = new ClientOpsMsgToClientCommandConverter(initialCapacity: 32);

            _clients = new List<int>(32);
            _clientSet = new HashSet<int>();
            _reliableStreams = new Dictionary<int, ServerReliableStream>(32);

            _serverSampleSeq = 1;
            _opsWriter = new NetDataWriter();

        }

        public void Start(int port)
        {
            _transport.Start(port);
        }

        public void Poll()
        {
            _transport.Poll();

            ProcessNewConnections();
            ProcessPackets();
        }

        public void TickOnce(TickContext ctx)
        {
            using TickFrame frame = _engine.TickOnce(ctx);

            // Hash is produced by the engine as part of the canonical Frame.
            uint worldHash = frame.StateHash;

            // Baseline cadence is adapter-owned.
            if ((frame.Tick % Protocol.BaselineIntervalTicks) == 0)
                SendBaselineToAll();

            // Reliable lane: only reliable RepOps (e.g., FlowFire) are encoded here.
            ConsumeReliableOps(frame);

            // Flush reliable streams (ack/replay lives here).
            FlushReliableStreams(frame.Tick, worldHash);

            // Sample lane: positions (latest-wins).
            SendSampleSnapshotToAll(frame, worldHash);
        }

        // -------------------------
        // Connection / packet processing
        // -------------------------

        private void ProcessNewConnections()
        {
            while (_transport.TryDequeueConnected(out int connId))
            {
                if (_clientSet.Add(connId))
                    _clients.Add(connId);

                if (!_reliableStreams.ContainsKey(connId))
                    _reliableStreams.Add(connId, new ServerReliableStream(ReliableMaxOpsBytesPerTick, ReliableMaxPendingPackets));

                SendWelcome(connId);
                SendBaseline(connId);
                ReplayReliableStream(connId);
            }
        }

        private void ProcessPackets()
        {
            while (_transport.TryReceive(out NetPacket packet))
            {
                if (packet.Lane != Lane.Reliable)
                    continue;

                if (MsgCodec.TryDecodeHello(packet.Payload, out Hello hello))
                {
                    if (hello.ProtocolVersion != ProtocolVersion.Current)
                    {
                        DisconnectClient(
                            packet.ConnId,
                            $"Protocol mismatch. Client={hello.ProtocolVersion} Server={ProtocolVersion.Current}");
                        continue;
                    }

                    SendWelcome(packet.ConnId);
                    SendBaseline(packet.ConnId);
                    ReplayReliableStream(packet.ConnId);
                    continue;
                }

                if (MsgCodec.TryDecodePing(packet.Payload, out PingMsg ping))
                {
                    PongMsg pong = new PongMsg(
                        pingId: ping.PingId,
                        clientTimeMsEcho: ping.ClientTimeMs,
                        serverTimeMs: _engine.ServerTimeMs,
                        serverTick: _engine.CurrentTick);

                    byte[] bytes = MsgCodec.EncodePong(pong);
                    _transport.Send(packet.ConnId, Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
                    continue;
                }

                if (MsgCodec.TryDecodeClientAck(packet.Payload, out ClientAckMsg ack))
                {
                    if (_reliableStreams.TryGetValue(packet.ConnId, out ServerReliableStream? stream) && stream != null)
                        stream.OnAck(ack.LastAckedReliableSeq);
                    continue;
                }

                if (MsgCodec.TryDecodeClientOps(packet.Payload, out ClientOpsMsg clientOps))
                {
                    // Convert client wire ops -> client commands -> engine commands.
                    List<ClientCommand> clientCommands = _converter.ConvertToNewList(clientOps);
                    if (clientCommands.Count > 0)
                    {
                        List<EngineCommand<EngineCommandType>> engineCommands =
                            ClientCommandToEngineCommandConverter.ConvertToNewList(clientCommands);

                        _engine.EnqueueCommands(
                            connId: packet.ConnId,
                            requestedClientTick: clientOps.ClientTick,
                            clientCmdSeq: clientOps.ClientCmdSeq,
                            commands: engineCommands);
                    }

                    // Ack receipt (command reliability layer).
                    // Note: In the current architecture, client sends ClientAckMsg for server->client ops.
                    // Server doesn't send acks for client->server commands in this design.
                    continue;
                }
            }
        }

        // -------------------------
        // Adapter-owned message builders
        // -------------------------

        private void SendWelcome(int connId)
        {
            byte[] bytes = MsgCodec.EncodeWelcome(_tickRateHz, _engine.CurrentTick);
            _transport.Send(connId, Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        private void SendBaseline(int connId)
        {
            Sim.Snapshot.GameSnapshot snap = _engine.BuildSnapshot();
            uint hash = _engine.ComputeStateHash();

            BaselineMsg msg = BaselineBuilder.Build(snap, _engine.CurrentTick, hash);
            byte[] bytes = MsgCodec.EncodeBaseline(msg);

            _transport.Send(connId, Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        private void SendBaselineToAll()
        {
            int k = 0;
            while (k < _clients.Count)
            {
                SendBaseline(_clients[k]);
                k++;
            }
        }

        private void ReplayReliableStream(int connId)
        {
            if (!_reliableStreams.TryGetValue(connId, out ServerReliableStream? stream) || stream == null)
                return;

            foreach (byte[] packetBytes in stream.GetUnackedPackets())
                _transport.Send(connId, Lane.Reliable, new ArraySegment<byte>(packetBytes, 0, packetBytes.Length));
        }

        // -------------------------
        // Engine -> Reliable (RepOps -> wire ops -> reliable stream)
        // -------------------------

        private void ConsumeReliableOps(in TickFrame frame)
        {
            // Only a subset of RepOps should be delivered reliably.
            // In this demo, we treat FlowFire as reliable and everything else as Sample.
            if (frame.Ops.Count == 0)
                return;

            EncodeReliableRepOpsToWire(frame.Ops.Span, out ushort opCount, out byte[] opsPayload);

            if (opCount == 0)
                return;

            // Broadcast reliable ops to all connected clients.
            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];
                if (_reliableStreams.TryGetValue(connId, out ServerReliableStream? stream) && stream != null)
                {
                    stream.AddOpsForTick(frame.Tick, opCount, opsPayload);
                }
                k++;
            }
        }

        private void EncodeReliableRepOpsToWire(ReadOnlySpan<RepOp> ops, out ushort opCount, out byte[] payload)
        {
            _opsWriter.Reset();
            opCount = 0;

            if (ops.Length > 0)
            {
                int i = 0;
                while (i < ops.Length)
                {
                    RepOp op = ops[i];

                    switch (op.Type)
                    {
                        case RepOpType.FlowFire:
                            // op.A = trigger (byte stored in int), op.B = param0
                            OpsWriter.WriteFlowFire(_opsWriter, (byte)op.A, op.B);
                            opCount++;
                            break;

                        case RepOpType.FlowSnapshot:
                            // Decode packed bytes from op.A
                            byte flowState = (byte)(op.A & 0xFF);
                            byte roundState = (byte)((op.A >> 8) & 0xFF);
                            byte lastCookMetTarget = (byte)((op.A >> 16) & 0xFF);
                            byte cookAttemptsUsed = (byte)((op.A >> 24) & 0xFF);

                            OpsWriter.WriteFlowSnapshot(
                                _opsWriter,
                                flowState,
                                roundState,
                                lastCookMetTarget,
                                cookAttemptsUsed,
                                op.B,  // levelIndex
                                op.C,  // roundIndex
                                op.D,  // selectedChefHatId
                                op.E,  // targetScore
                                op.F,  // cumulativeScore
                                op.G,  // cookResultSeq
                                op.H); // lastCookScoreDelta
                            opCount++;
                            break;

                        default:
                            break;
                    }

                    i++;
                }
            }

            payload = (opCount == 0) ? Array.Empty<byte>() : _opsWriter.CopyData();
        }

        private void FlushReliableStreams(int serverTick, uint worldHash)
        {
            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];

                if (_reliableStreams.TryGetValue(connId, out ServerReliableStream? stream) && stream != null)
                {
                    byte[] packetBytes = stream.FlushToPacketIfAny(serverTick, worldHash);
                    if (packetBytes != null)
                        _transport.Send(connId, Lane.Reliable, new ArraySegment<byte>(packetBytes, 0, packetBytes.Length));
                }

                k++;
            }
        }

        // -------------------------
        // Engine -> Sample (RepOps -> wire ops -> broadcast)
        // -------------------------

        private void SendSampleSnapshotToAll(in TickFrame frame, uint worldHash)
        {
            _opsWriter.Reset();
            ushort opCount = 0;

            ReadOnlySpan<RepOp> ops = frame.Ops.Span;

            int i = 0;
            while (i < ops.Length)
            {
                RepOp op = ops[i];
                if (op.Type == RepOpType.PositionAt)
                {
                    // op.A=entityId, op.B=x, op.C=y
                    OpsWriter.WritePositionAt(_opsWriter, op.A, op.B, op.C);
                    opCount++;
                }
                i++;
            }

            // Send heartbeat even when no ops (prevents interpolation gaps)
            // If opCount == 0, we still send a message with opCount=0 to maintain tick cadence
            byte[] opsBytes = (opCount == 0) ? Array.Empty<byte>() : _opsWriter.CopyData();

            ServerOpsMsg msg = new ServerOpsMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                frame.Tick,
                _serverSampleSeq++,
                worldHash,
                opCount,
                opsBytes);

            byte[] packetBytes = MsgCodec.EncodeServerOps(Lane.Sample, msg);

            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];
                _transport.Send(connId, Lane.Sample, new ArraySegment<byte>(packetBytes, 0, packetBytes.Length));
                k++;
            }
        }

        private void DisconnectClient(int connId, string reason)
        {
            Console.WriteLine($"[Net] Disconnecting connId={connId}. {reason}");
            _transport.Disconnect(connId, reason);
            _clientSet.Remove(connId);
            _clients.Remove(connId);
            _reliableStreams.Remove(connId);
        }

    }
}
