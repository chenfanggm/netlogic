using System;
using System.Collections.Generic;
using Game;
using LiteNetLib.Utils;
using Net;

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

        private readonly NetDataWriter _opsWriter;
        private readonly NetDataWriter _msgWriter;

        private readonly List<int> _connectedClients;

        public int Tick => _clock.Tick;

        public GameServer(IServerTransport transport, TickClock clock, World world)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _world = world ?? throw new ArgumentNullException(nameof(world));

            _opsWriter = new NetDataWriter();
            _msgWriter = new NetDataWriter();

            _connectedClients = new List<int>(16);
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
                if (!_connectedClients.Contains(connId))
                    _connectedClients.Add(connId);

                // Ensure at least one entity exists for demo/testing
                if (_world.TryGetEntity(1, out Entity ignored) == false)
                {
                    _world.CreateEntityAt(entityId: 1, x: 0, y: 0);
                }
            }

            NetPacket packet;
            while (_transport.TryReceive(out packet))
            {
                if (packet.Lane != Lane.Reliable)
                    continue;

                // Decode ClientOpsMsg from Reliable lane only
                ClientOpsMsg msg;
                bool ok = MsgCodec.TryDecodeClientOps(packet.Payload, out msg);
                if (!ok)
                    continue;

                ApplyClientOps(packet.ConnectionId, msg);
            }
        }

        /// <summary>
        /// Drive exactly one tick of simulation (caller controls scheduling).
        /// </summary>
        public void TickOnce()
        {
            // Run fixed tick
            _clock.Advance(1);

            // TODO: server authoritative simulation systems should run here

            // Send ops on both lanes
            SendReliableOps(); // currently empty hook (container ops, events)
            SendSampleOps();   // position samples
        }

        private void ApplyClientOps(int connectionId, ClientOpsMsg msg)
        {
            // Parse ops payload using NetDataReader
            NetDataReader r = new NetDataReader(msg.OpsPayload.Array, msg.OpsPayload.Offset, msg.OpsPayload.Count);

            int i = 0;
            while (i < msg.OpCount)
            {
                OpType t = OpsReader.ReadOpType(r);

                if (t == OpType.MoveBy)
                {
                    int entityId = r.GetInt();
                    int dx = r.GetInt();
                    int dy = r.GetInt();

                    // Authoritative move (server decides)
                    _world.TryMoveEntityBy(entityId, dx, dy);
                }
                else
                {
                    // Unknown op type (ignore)
                    // If you need forward compatibility, add op-length fields.
                    break;
                }

                i++;
            }
        }

        private void SendReliableOps()
        {
            // Reliable lane is for:
            // - container ops (hand/deck/discard)
            // - discrete events (spawn/despawn, ability cast, etc.)
            //
            // This demo doesn't emit reliable server ops yet, but the hook is here.

            _opsWriter.Reset();
            ushort opCount = 0;

            if (opCount == 0)
                return;

            ServerOpsMsg msg = new ServerOpsMsg(_clock.Tick, Lane.Reliable, opCount, new ArraySegment<byte>(_opsWriter.Data, 0, _opsWriter.Length));
            byte[] bytes = MsgCodec.EncodeServerOps(msg);

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes, 0, bytes.Length);

            int k = 0;
            while (k < _connectedClients.Count)
            {
                int connId = _connectedClients[k];
                _transport.Send(connId, Lane.Reliable, payload);
                k++;
            }
        }

        private void SendSampleOps()
        {
            // Sample lane is for:
            // - PositionAt / VelocityAt samples
            // - frequently changing stats (optional)
            //
            // These are "latest wins" on client; safe to drop.

            _opsWriter.Reset();
            ushort opCount = 0;

            foreach (Entity e in _world.Entities)
            {
                OpsWriter.WritePositionAt(_opsWriter, e.Id, e.X, e.Y);
                opCount++;
            }

            if (opCount == 0)
                return;

            ServerOpsMsg msg = new ServerOpsMsg(_clock.Tick, Lane.Sample, opCount, new ArraySegment<byte>(_opsWriter.Data, 0, _opsWriter.Length));
            byte[] bytes = MsgCodec.EncodeServerOps(msg);

            ArraySegment<byte> payload = new ArraySegment<byte>(bytes, 0, bytes.Length);

            int k = 0;
            while (k < _connectedClients.Count)
            {
                int connId = _connectedClients[k];
                _transport.Send(connId, Lane.Sample, payload);
                k++;
            }
        }
    }
}
