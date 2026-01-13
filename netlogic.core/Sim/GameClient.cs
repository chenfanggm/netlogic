using System;
using System.Collections.Generic;
using System.Diagnostics;
using Net;
using LiteNetLib.Utils;

namespace Sim
{
    /// <summary>
    /// Transport-agnostic game client:
    /// - Owns IClientTransport
    /// - Decodes ServerOpsMsg from both lanes
    /// - Applies Reliable ops immediately
    /// - Feeds Sample ops into interpolation buffers
    /// </summary>
    public sealed class GameClient
    {
        private readonly IClientTransport _transport;
        private readonly int _tickRateHz;

        private readonly SnapshotRingBuffer _snapshots;
        private readonly RenderInterpolator _interpolator;

        private readonly Stopwatch _clientTime;
        private readonly RttEstimator _rtt;
        private readonly InputDelayController _delay;

        private int _lastSampleServerTick;
        private uint _lastAppliedReliableSeq;

        private uint _nextPingId;
        private int _nextPingAtClientTick;

        private uint _clientCmdSeq;
        private readonly NetDataWriter _opsWriter;

        private bool _handshakeDone;

        public bool IsConnected => _transport.IsConnected;

        public int RenderDelayTicks => _delay.RenderDelayTicks;
        public int InputDelayTicks => _delay.InputDelayTicks;

        public GameClient(IClientTransport transport, int tickRateHz)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _tickRateHz = tickRateHz;

            _snapshots = new SnapshotRingBuffer(capacity: 256);
            _interpolator = new RenderInterpolator();

            _clientTime = Stopwatch.StartNew();
            _rtt = new RttEstimator();
            _delay = new InputDelayController(tickRateHz);

            _lastSampleServerTick = -1;
            _lastAppliedReliableSeq = 0;

            _nextPingId = 1;
            _nextPingAtClientTick = 0;

            _clientCmdSeq = 1;
            _opsWriter = new NetDataWriter();

            _handshakeDone = false;
        }

        public void Start()
        {
            _transport.Start();
        }

        public void Connect(string host, int port)
        {
            _transport.Connect(host, port);
        }

        public void Poll(int clientTick)
        {
            _transport.Poll();

            if (IsConnected && !_handshakeDone)
            {
                byte[] hello = MsgCodec.EncodeHello(_tickRateHz);
                _transport.Send(Lane.Reliable, new ArraySegment<byte>(hello, 0, hello.Length));
                _handshakeDone = true;
            }

            if (IsConnected && clientTick >= _nextPingAtClientTick)
            {
                SendPing(clientTick);
                _nextPingAtClientTick = clientTick + Protocol.PingIntervalTicks;
            }

            NetPacket packet;
            while (_transport.TryReceive(out packet))
            {
                // All control messages travel on Reliable lane
                if (packet.Lane == Lane.Reliable)
                {
                    Welcome welcome;
                    if (MsgCodec.TryDecodeWelcome(packet.Payload, out welcome))
                    {
                        // You can use welcome.ServerTickRateHz if you want
                        continue;
                    }

                    PongMsg pong;
                    if (MsgCodec.TryDecodePong(packet.Payload, out pong))
                    {
                        long nowMs = _clientTime.ElapsedMilliseconds;
                        double rttMs = (double)(nowMs - pong.ClientTimeMsEcho);
                        _rtt.AddSample(rttMs);
                        _delay.UpdateFromRttMs(_rtt.SmoothedRttMs);
                        continue;
                    }

                    BaselineMsg baseMsg;
                    if (MsgCodec.TryDecodeBaseline(packet.Payload, out baseMsg))
                    {
                        ApplyBaseline(baseMsg);
                        continue;
                    }

                    ServerOpsMsg rel;
                    if (MsgCodec.TryDecodeServerOps(packet.Payload, out rel))
                    {
                        ApplyReliableOps(rel);
                        continue;
                    }
                }

                // Sample lane: ServerOps(Sample)
                if (packet.Lane == Lane.Sample)
                {
                    ServerOpsMsg samp;
                    if (MsgCodec.TryDecodeServerOps(packet.Payload, out samp))
                    {
                        ApplySampleOps(samp);
                        continue;
                    }
                }
            }
        }

        public void SendMoveBy(int clientTick, int entityId, int dx, int dy)
        {
            if (!IsConnected)
                return;

            // Apply input delay ticks (client-side scheduling)
            int delayedTick = clientTick + _delay.InputDelayTicks;

            _opsWriter.Reset();
            ushort opCount = 0;

            OpsWriter.WriteMoveBy(_opsWriter, entityId, dx, dy);
            opCount++;

            byte[] opsBytes = _opsWriter.CopyData();

            ClientOpsMsg msg = new ClientOpsMsg(
                clientTick: delayedTick,
                clientCmdSeq: _clientCmdSeq++,
                opCount: opCount,
                opsPayload: opsBytes);

            byte[] packet = MsgCodec.EncodeClientOps(msg);
            _transport.Send(Lane.Reliable, new ArraySegment<byte>(packet, 0, packet.Length));
        }

        public EntityState[] GetRenderEntities()
        {
            if (_lastSampleServerTick < 0)
                return Array.Empty<EntityState>();

            double renderTick = (double)_lastSampleServerTick - (double)_delay.RenderDelayTicks;

            SnapshotMsg a;
            SnapshotMsg b;
            double alpha;

            bool ok = _snapshots.TryGetInterpolationPair(renderTick, out a, out b, out alpha);
            if (!ok)
                return Array.Empty<EntityState>();

            return _interpolator.Interpolate(a, b, alpha);
        }

        private void SendPing(int clientTick)
        {
            PingMsg ping = new PingMsg(
                pingId: _nextPingId++,
                clientTimeMs: _clientTime.ElapsedMilliseconds,
                clientTick: clientTick);

            byte[] bytes = MsgCodec.EncodePing(ping);
            _transport.Send(Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        private void ApplyBaseline(BaselineMsg msg)
        {
            // Full authoritative reset: clear interpolation and seed with baseline
            _snapshots.Clear();

            SnapshotMsg snap = new SnapshotMsg(msg.ServerTick, msg.Entities);
            _snapshots.Add(snap);

            _lastSampleServerTick = msg.ServerTick;
        }

        private void ApplyReliableOps(ServerOpsMsg msg)
        {
            // Idempotency guard (even if transport is reliable)
            if (msg.ServerSeq <= _lastAppliedReliableSeq)
                return;

            _lastAppliedReliableSeq = msg.ServerSeq;

            // Parse reliable ops here (container ops/events)
            // Currently empty hook.
            // If you add ops: decode with NetDataReader and op-length skipping exactly like sample parsing.
        }

        private void ApplySampleOps(ServerOpsMsg msg)
        {
            if (msg.ServerTick <= _lastSampleServerTick)
                return;

            int gap = msg.ServerTick - _lastSampleServerTick;
            if (_lastSampleServerTick >= 0 && gap > Protocol.MaxSampleTickGapBeforeSnap)
            {
                // Catch-up mode: snap (skip smoothing)
                _snapshots.Clear();
            }

            _lastSampleServerTick = msg.ServerTick;

            NetDataReader r = new NetDataReader(msg.OpsPayload, 0, msg.OpsPayload.Length);

            EntityState[] entities = new EntityState[msg.OpCount];

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
                    entities[i] = new EntityState(id, x, y, 0);
                }
                else
                {
                    OpsReader.SkipBytes(r, opLen);
                }

                i++;
            }

            SnapshotMsg snap = new SnapshotMsg(msg.ServerTick, entities);
            _snapshots.Add(snap);
        }
    }
}
