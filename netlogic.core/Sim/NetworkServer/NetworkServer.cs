using System;
using System.Collections.Generic;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.serverengine.replication;
using LiteNetLib.Utils;
using com.aqua.netlogic.net;
using com.aqua.netlogic.net.transport;
using com.aqua.netlogic.sim.clientengine.protocol;
using com.aqua.netlogic.sim.networkserver.reliability;
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.timing;

namespace com.aqua.netlogic.sim.networkserver
{
    /// <summary>
    /// Thin network adapter:
    /// - Owns transport
    /// - Owns handshake/ping/ack/baseline
    /// - Converts wire ClientOps -> ClientCommand[]
    /// - Converts ClientCommand[] -> EngineCommand[]
    /// - Feeds ServerEngine
    /// - Calls TickOnce()
    /// - Hashes + encodes TickFrame into wire packets (Reliable + Unreliable)
    /// </summary>
    public sealed class NetworkServer
    {
        private readonly com.aqua.netlogic.net.transport.IServerTransport _transport;
        private readonly ServerEngine _engine;
        private readonly int _tickRateHz;

        private readonly ClientOpsMsgToClientCommandConverter _converter;
        private readonly List<int> _clients;
        private readonly HashSet<int> _clientSet;
        private readonly Dictionary<int, ServerReliableStream> _reliableStreams;
        private double _lastServerTimeMs;

        private const int ReliableMaxOpsBytesPerTick = 8 * 1024;
        private const int ReliableMaxPendingPackets = 128;

        private uint _serverUnreliableSeq;
        private readonly NetDataWriter _opsWriter;
        private readonly global::System.Buffers.ArrayBufferWriter<byte> _packetWriter =
            new global::System.Buffers.ArrayBufferWriter<byte>(2048);

        private readonly ServerNetMetrics _netMetrics = new ServerNetMetrics();

        // Baseline policy
        private readonly int _baselineEveryTicks = 60;
        private readonly int _baselineCooldownTicks = 10;
        private readonly Dictionary<int, int> _nextPeriodicBaselineTick = new();
        private readonly Dictionary<int, int> _nextAllowedTriggeredBaselineTick = new();

        // Optional: print metrics periodically
        private int _nextMetricsPrintTick = 0;
        private readonly int _metricsPrintEveryTicks = 120;


        public int CurrentServerTick => _engine.CurrentTick;
        public int TickRateHz => _tickRateHz;

        public NetworkServer(com.aqua.netlogic.net.transport.IServerTransport transport, int tickRateHz, com.aqua.netlogic.sim.game.Game initialGame)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (tickRateHz <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRateHz));

            _tickRateHz = tickRateHz;
            _engine = new ServerEngine(initialGame ?? throw new ArgumentNullException(nameof(initialGame)));

            _converter = new ClientOpsMsgToClientCommandConverter(initialCapacity: 32);

            _clients = new List<int>(32);
            _clientSet = new HashSet<int>();
            _reliableStreams = new Dictionary<int, ServerReliableStream>(32);

            _serverUnreliableSeq = 1;
            _opsWriter = new NetDataWriter();
            _lastServerTimeMs = 0;

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
            _lastServerTimeMs = ctx.ServerTimeMs;
            _netMetrics.Tick(ctx.ServerTimeMs);
            using TickFrame frame = _engine.TickOnce(ctx);

            // Hash is produced by the engine as part of the canonical Frame.
            uint worldHash = frame.StateHash;

            using RepOpPartitioner.PartitionedOps part = RepOpPartitioner.Partition(frame.Ops.Span);

            RunPeriodicBaselines(CurrentServerTick);
            MaybePrintNetMetrics(CurrentServerTick);

            // Reliable lane: only reliable RepOps (e.g., FlowFire) are encoded here.
            ConsumeReliableOps(frame.Tick, part.Reliable);

            // Flush reliable streams (ack/replay lives here).
            FlushReliableStreams(frame.Tick, worldHash);

            // Unreliable lane: positions (latest-wins).
            SendUnreliableSnapshotToAll(frame.Tick, worldHash, part.Unreliable);
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

                _nextPeriodicBaselineTick[connId] = CurrentServerTick + _baselineEveryTicks;
                _nextAllowedTriggeredBaselineTick[connId] = CurrentServerTick;
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
                    _nextPeriodicBaselineTick[packet.ConnId] = CurrentServerTick + _baselineEveryTicks;
                    _nextAllowedTriggeredBaselineTick[packet.ConnId] = CurrentServerTick;
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
                    SendPacket(packet.ConnId, Lane.Reliable, bytes);
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
            byte[] bytes = MsgCodec.EncodeWelcome(_tickRateHz, _engine.CurrentTick, _lastServerTimeMs);
            SendPacket(connId, Lane.Reliable, bytes);
        }

        public NetHealthStats GetNetHealthStats()
        {
            _engine.GetCommandBufferStats(
                out long droppedTooOld,
                out long snappedLate,
                out long clampedFuture,
                out long accepted);

            return new NetHealthStats(droppedTooOld, snappedLate, clampedFuture, accepted);
        }

        private void SendBaseline(int connId)
        {
            com.aqua.netlogic.sim.game.snapshot.GameSnapshot snap = _engine.BuildSnapshot();
            uint hash = _engine.ComputeStateHash();

            BaselineMsg msg = BaselineBuilder.Build(snap, _engine.CurrentTick, hash);
            byte[] bytes = MsgCodec.EncodeBaseline(msg);

            SendPacket(connId, Lane.Reliable, bytes);
            _netMetrics.RecordBaselineSent();
            _nextPeriodicBaselineTick[connId] = CurrentServerTick + _baselineEveryTicks;
            _nextAllowedTriggeredBaselineTick[connId] = CurrentServerTick + _baselineCooldownTicks;
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

            foreach (ArraySegment<byte> seg in stream.GetUnackedPacketSegments())
                SendPacket(connId, Lane.Reliable, seg);
        }

        // -------------------------
        // Engine -> Reliable (RepOps -> wire ops -> reliable stream)
        // -------------------------

        private void ConsumeReliableOps(int frameTick, ReadOnlySpan<RepOp> ops)
        {
            // Reliable-only RepOps (already partitioned by policy).
            if (ops.Length == 0)
                return;

            EncodeReliableRepOpsToWire(ops, out ushort opCount);

            if (opCount == 0)
                return;

            // Broadcast reliable ops to all connected clients.
            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];
                if (_reliableStreams.TryGetValue(connId, out ServerReliableStream? stream) && stream != null)
                {
                    byte[] opsPayload = com.aqua.netlogic.net.PooledBytes.RentCopy(_opsWriter, out int opsLen);
                    try
                    {
                        stream.AddOpsForTick(frameTick, opCount, opsPayload, opsLen);
                    }
                    catch
                    {
                        if (!ReferenceEquals(opsPayload, Array.Empty<byte>()))
                            global::System.Buffers.ArrayPool<byte>.Shared.Return(opsPayload, clearArray: false);
                        throw;
                    }
                }
                k++;
            }
        }

        private void EncodeReliableRepOpsToWire(ReadOnlySpan<RepOp> ops, out ushort opCount)
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
                        case RepOpType.EntitySpawned:
                            // op.A = entityId, op.B = x, op.C = y, op.D = hp
                            OpsWriter.WriteEntitySpawned(_opsWriter, op.A, op.B, op.C, op.D);
                            opCount++;
                            break;

                        case RepOpType.EntityDestroyed:
                            // op.A = entityId
                            OpsWriter.WriteEntityDestroyed(_opsWriter, op.A);
                            opCount++;
                            break;

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

        }

        private void FlushReliableStreams(int serverTick, uint worldHash)
        {
            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];

                if (_reliableStreams.TryGetValue(connId, out ServerReliableStream? stream) && stream != null)
                {
                    ArraySegment<byte> seg = stream.FlushToPacketIfAny(serverTick, worldHash);
                    if (seg.Array != null && seg.Count > 0)
                        SendPacket(connId, Lane.Reliable, seg);
                }

                k++;
            }
        }

        // -------------------------
        // Engine -> Unreliable (RepOps -> wire ops -> broadcast)
        // -------------------------

        private void SendUnreliableSnapshotToAll(int serverTick, uint worldHash, ReadOnlySpan<RepOp> ops)
        {
            _opsWriter.Reset();
            ushort opCount = 0;

            int i = 0;
            while (i < ops.Length)
            {
                RepOp op = ops[i];
                if (op.Type == RepOpType.PositionSnapshot)
                {
                    // op.A=entityId, op.B=x, op.C=y
                    OpsWriter.WritePositionSnapshot(_opsWriter, op.A, op.B, op.C);
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
                serverTick,
                _serverUnreliableSeq++,
                worldHash,
                opCount,
                opsBytes);

            ArraySegment<byte> packetSeg = MsgCodecFast.EncodeServerOpsToBuffer(_packetWriter, msg);

            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];
                SendPacket(connId, Lane.Unreliable, packetSeg);
                k++;
            }
        }

        public void RequestBaseline(int connId, string reason)
        {
            int now = CurrentServerTick;

            if (!_nextAllowedTriggeredBaselineTick.TryGetValue(connId, out int allowedAt))
                allowedAt = now;

            if (now < allowedAt)
                return;

            Console.WriteLine($"[Net] Baseline requested conn={connId} tick={now} reason={reason}");

            SendBaseline(connId);
            ReplayReliableStream(connId);

            _nextAllowedTriggeredBaselineTick[connId] = now + _baselineCooldownTicks;
        }

        private void RunPeriodicBaselines(int nowTick)
        {
            if (_baselineEveryTicks <= 0)
                return;

            List<int> keys = new List<int>(_nextPeriodicBaselineTick.Keys);
            int i = 0;
            while (i < keys.Count)
            {
                int connId = keys[i];
                if (_nextPeriodicBaselineTick.TryGetValue(connId, out int dueTick) && nowTick >= dueTick)
                    SendBaseline(connId);
                i++;
            }
        }

        private void MaybePrintNetMetrics(int nowTick)
        {
            if (nowTick < _nextMetricsPrintTick)
                return;

            _nextMetricsPrintTick = nowTick + _metricsPrintEveryTicks;

            ServerNetMetrics.Snapshot snap = _netMetrics.GetSnapshot();
            CommandIngressStats stats = GetCommandIngressStats();

            Console.WriteLine(
                $"[NetStats] tick={nowTick} " +
                $"relBps(avg60s)={snap.AvgReliableBytesPerSec:0.0} " +
                $"unrelBps(avg60s)={snap.AvgUnreliableBytesPerSec:0.0} " +
                $"baselines(last60s)={snap.BaselinesTotalLast60s} min/s={snap.BaselinesMinPerSec} max/s={snap.BaselinesMaxPerSec} " +
                $"cmd.drop_old={stats.DroppedTooOld} cmd.snap_late={stats.SnappedLate} cmd.clamp_future={stats.ClampedFuture}");
        }

        private CommandIngressStats GetCommandIngressStats()
        {
            _engine.GetCommandBufferStats(
                out long droppedTooOld,
                out long snappedLate,
                out long clampedFuture,
                out _);

            return new CommandIngressStats(droppedTooOld, snappedLate, clampedFuture);
        }

        private void SendPacket(int connId, Lane lane, byte[] payload)
        {
            if (payload == null)
                return;

            _transport.Send(connId, lane, new ArraySegment<byte>(payload, 0, payload.Length));

            if (lane == Lane.Reliable)
                _netMetrics.RecordReliableBytes(payload.Length);
            else
                _netMetrics.RecordUnreliableBytes(payload.Length);
        }

        private void SendPacket(int connId, Lane lane, ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count <= 0)
                return;

            _transport.Send(connId, lane, payload);

            if (lane == Lane.Reliable)
                _netMetrics.RecordReliableBytes(payload.Count);
            else
                _netMetrics.RecordUnreliableBytes(payload.Count);
        }

        private void DisconnectClient(int connId, string reason)
        {
            Console.WriteLine($"[Net] Disconnecting connId={connId}. {reason}");
            _transport.Disconnect(connId, reason);
            _clientSet.Remove(connId);
            _clients.Remove(connId);
            _reliableStreams.Remove(connId);
            _nextPeriodicBaselineTick.Remove(connId);
            _nextAllowedTriggeredBaselineTick.Remove(connId);
        }

    }
}
