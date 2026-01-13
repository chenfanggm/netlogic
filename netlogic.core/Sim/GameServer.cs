using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game;
using Net;
using LiteNetLib.Utils;

namespace Sim
{
    /// <summary>
    /// Transport-agnostic game server:
    /// - Owns IServerTransport
    /// - Runs a fixed-tick loop (driven by caller)
    /// - Decodes ClientOpsMsg from Reliable lane only
    /// - Encodes/sends ServerOpsMsg on both lanes (Reliable + Sample)
    /// </summary>
    public sealed class GameServer
    {
        private readonly IServerTransport _transport;
        private readonly TickClock _clock;
        private readonly World _world;

        private readonly List<int> _clients;

        private uint _serverReliableSeq;
        private uint _serverSampleSeq;

        private readonly Stopwatch _serverTime;

        private readonly NetDataWriter _opsWriter;

        public int Tick => _clock.Tick;

        public GameServer(IServerTransport transport, TickClock clock, World world)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _world = world ?? throw new ArgumentNullException(nameof(world));

            _clients = new List<int>(32);

            _serverReliableSeq = 1;
            _serverSampleSeq = 1;

            _serverTime = Stopwatch.StartNew();

            _opsWriter = new NetDataWriter();
        }

        public void Start(int port)
        {
            _transport.Start(port);
        }

        public void Poll()
        {
            _transport.Poll();

            int connId;
            while (_transport.TryDequeueConnected(out connId))
            {
                if (!_clients.Contains(connId))
                    _clients.Add(connId);

                // Immediately send Welcome + Baseline (join-in-progress ready)
                SendWelcome(connId);
                SendBaseline(connId);
            }

            NetPacket packet;
            while (_transport.TryReceive(out packet))
            {
                // All control and commands are Reliable lane only
                if (packet.Lane != Lane.Reliable)
                    continue;

                // HELLO
                Hello hello;
                if (MsgCodec.TryDecodeHello(packet.Payload, out hello))
                {
                    // Validate protocol/version
                    // (We already validate in decoder)
                    SendWelcome(packet.ConnectionId);
                    SendBaseline(packet.ConnectionId);
                    continue;
                }

                // PING -> respond PONG
                PingMsg ping;
                if (MsgCodec.TryDecodePing(packet.Payload, out ping))
                {
                    PongMsg pong = new PongMsg(
                        pingId: ping.PingId,
                        clientTimeMsEcho: ping.ClientTimeMs,
                        serverTimeMs: _serverTime.ElapsedMilliseconds,
                        serverTick: _clock.Tick);

                    byte[] pongBytes = MsgCodec.EncodePong(pong);
                    _transport.Send(packet.ConnectionId, Lane.Reliable, new ArraySegment<byte>(pongBytes, 0, pongBytes.Length));
                    continue;
                }

                // CLIENT OPS
                ClientOpsMsg cmd;
                if (MsgCodec.TryDecodeClientOps(packet.Payload, out cmd))
                {
                    ApplyClientOps(packet.ConnectionId, cmd);
                    continue;
                }
            }
        }

        public void TickOnce()
        {
            _clock.Advance(1);

            // TODO: run authoritative systems here (AI, combat, grid collisions, etc.)

            // Baseline snapshots at cadence for recovery & idempotency
            if ((_clock.Tick % Protocol.BaselineIntervalTicks) == 0)
            {
                SendBaselineToAll();
            }

            // Reliable server ops (container ops/events) - currently empty hook
            SendReliableOpsToAll();

            // Sample server ops (positions) - latest wins
            SendSampleOpsToAll();
        }

        private void ApplyClientOps(int connectionId, ClientOpsMsg msg)
        {
            NetDataReader r = new NetDataReader(msg.OpsPayload.Array, msg.OpsPayload.Offset, msg.OpsPayload.Count);

            int i = 0;
            while (i < msg.OpCount)
            {
                OpType opType = OpsReader.ReadOpType(r);
                ushort opLen = OpsReader.ReadOpLen(r);

                if (opType == OpType.MoveBy)
                {
                    int entityId = r.GetInt();
                    int dx = r.GetInt();
                    int dy = r.GetInt();

                    // Authoritative move (validate + collide here)
                    _world.TryMoveEntityBy(entityId, dx, dy);
                }
                else
                {
                    OpsReader.SkipBytes(r, opLen);
                }

                i++;
            }
        }

        private void SendWelcome(int connId)
        {
            byte[] bytes = MsgCodec.EncodeWelcome(_clock.TickRateHz, _clock.Tick);
            _transport.Send(connId, Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        private void SendBaseline(int connId)
        {
            EntityState[] entities = _world.ToSnapshot();
            uint hash = StateHash.ComputeEntitiesHash(entities);

            BaselineMsg msg = new BaselineMsg(_clock.Tick, hash, entities);
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

        private void SendReliableOpsToAll()
        {
            _opsWriter.Reset();
            ushort opCount = 0;

            // TODO: write reliable ops here (container ops/events)
            // Example: OpsWriter.WriteCardMove(...)

            if (opCount == 0)
                return;

            uint hash = StateHash.ComputeWorldHash(_world);

            ServerOpsMsg msg = new ServerOpsMsg(
                serverTick: _clock.Tick,
                lane: Lane.Reliable,
                serverSeq: _serverReliableSeq++,
                stateHash: hash,
                opCount: opCount,
                opsPayload: new ArraySegment<byte>(_opsWriter.Data, 0, _opsWriter.Length));

            byte[] bytes = MsgCodec.EncodeServerOps(msg);

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes, 0, bytes.Length);

            int k = 0;
            while (k < _clients.Count)
            {
                _transport.Send(_clients[k], Lane.Reliable, payload);
                k++;
            }
        }

        private void SendSampleOpsToAll()
        {
            _opsWriter.Reset();
            ushort opCount = 0;

            foreach (Entity e in _world.Entities)
            {
                OpsWriter.WritePositionAt(_opsWriter, e.Id, e.X, e.Y);
                opCount++;
            }

            if (opCount == 0)
                return;

            uint hash = StateHash.ComputeWorldHash(_world);

            ServerOpsMsg msg = new ServerOpsMsg(
                serverTick: _clock.Tick,
                lane: Lane.Sample,
                serverSeq: _serverSampleSeq++,
                stateHash: hash,
                opCount: opCount,
                opsPayload: new ArraySegment<byte>(_opsWriter.Data, 0, _opsWriter.Length));

            byte[] bytes = MsgCodec.EncodeServerOps(msg);

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes, 0, bytes.Length);

            int k = 0;
            while (k < _clients.Count)
            {
                _transport.Send(_clients[k], Lane.Sample, payload);
                k++;
            }
        }
    }
}
