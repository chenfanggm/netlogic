using Sim.Game;
using Sim.Engine;
using LiteNetLib.Utils;
using Net;
using Sim.Client.Commands;
using Sim.Server.Reliability;
using Sim.Client.Command;
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
    /// - Hashes + encodes EngineTickResult into wire packets (Reliable + Sample)
    /// </summary>
    public sealed class GameServer
    {
        private readonly IServerTransport _transport;
        private readonly GameEngine _engine;
        private readonly int _tickRateHz;

        private readonly ClientOpsMsgToClientCommandConverter _converter;
        private readonly List<int> _clients;
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
            EngineTickResult tick = _engine.TickOnce(ctx);

            // Adapter-owned hashing (engine does not hash).
            Game.Game world = _engine.ReadOnlyWorld;
            uint worldHash = StateHash.ComputeWorldHash(world);

            // Baseline cadence is adapter-owned.
            if ((tick.ServerTick % Protocol.BaselineIntervalTicks) == 0)
                SendBaselineToAll();

            // Encode engine discrete ops into reliable streams (if any).
            ConsumeReliableOps(tick);

            // Flush reliable streams (ack/replay lives here).
            FlushReliableStreams(tick.ServerTick, worldHash);

            // Encode continuous snapshot into Sample lane.
            SendSampleSnapshotToAll(tick, worldHash);
        }

        // -------------------------
        // Connection / packet processing
        // -------------------------

        private void ProcessNewConnections()
        {
            while (_transport.TryDequeueConnected(out int connId))
            {
                if (!_clients.Contains(connId))
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

                if (MsgCodec.TryDecodeHello(packet.Payload, out Hello _))
                {
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

                if (MsgCodec.TryDecodeClientOps(packet.Payload, out ClientOpsMsg ops))
                {
                    List<ClientCommand> clientCommands = _converter.ConvertToNewList(ops);
                    List<EngineCommand<EngineCommandType>> engineCommands =
                        ClientCommandToEngineCommandConverter.ConvertToNewList(clientCommands);

                    _engine.EnqueueCommands(
                        connId: packet.ConnId,
                        requestedClientTick: ops.ClientTick,
                        clientCmdSeq: ops.ClientCmdSeq,
                        commands: engineCommands);

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
            Game.Game world = _engine.ReadOnlyWorld;
            EntityState[] entities = world.ToSnapshot();
            uint hash = StateHash.ComputeEntitiesHash(entities);

            BaselineMsg msg = new BaselineMsg(_engine.CurrentTick, hash, entities);
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
        // Engine -> Reliable (domain ops -> wire ops -> reliable stream)
        // -------------------------

        private void ConsumeReliableOps(in EngineTickResult tick)
        {
            if (tick.ReliableOps == null || tick.ReliableOps.Length == 0)
                return;

            int i = 0;
            while (i < tick.ReliableOps.Length)
            {
                EngineOpBatch batch = tick.ReliableOps[i];

                if (_reliableStreams.TryGetValue(batch.ConnId, out ServerReliableStream? stream) && stream != null)
                {
                    // Encode engine ops into wire ops payload.
                    // NOTE: Currently EngineOpType is empty in this demo, so this does nothing.
                    EncodeEngineOpsToWire(batch.Ops, out ushort opCount, out byte[] opsPayload);

                    if (opCount > 0)
                        stream.AddOpsForTick(tick.ServerTick, opCount, opsPayload);
                }

                i++;
            }
        }

        private void EncodeEngineOpsToWire(EngineOp[] ops, out ushort opCount, out byte[] payload)
        {
            _opsWriter.Reset();
            opCount = 0;

            if (ops != null)
            {
                int i = 0;
                while (i < ops.Length)
                {
                    EngineOp op = ops[i];

                    // Map EngineOpType -> wire ops via OpsWriter.
                    // Add cases as you introduce discrete systems.
                    switch (op.Type)
                    {
                        case EngineOpType.None:
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
        // Engine -> Sample (snapshot -> wire ops -> broadcast)
        // -------------------------

        private void SendSampleSnapshotToAll(in EngineTickResult tick, uint worldHash)
        {
            _opsWriter.Reset();
            ushort opCount = 0;

            SampleEntityPos[] snap = tick.Snapshot.Entities;

            int i = 0;
            while (i < snap.Length)
            {
                SampleEntityPos s = snap[i];
                OpsWriter.WritePositionAt(_opsWriter, s.EntityId, s.X, s.Y);
                opCount++;
                i++;
            }

            if (opCount == 0)
                return;

            byte[] opsBytes = _opsWriter.CopyData();

            ServerOpsMsg msg = new ServerOpsMsg(
                serverTick: tick.ServerTick,
                serverSeq: _serverSampleSeq++,
                stateHash: worldHash,
                opCount: opCount,
                opsPayload: opsBytes);

            byte[] packetBytes = MsgCodec.EncodeServerOps(Lane.Sample, msg);

            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];
                _transport.Send(connId, Lane.Sample, new ArraySegment<byte>(packetBytes, 0, packetBytes.Length));
                k++;
            }
        }
    }
}
