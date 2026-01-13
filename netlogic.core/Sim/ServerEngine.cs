using System;
using System.Collections.Generic;
using Game;
using LiteNetLib.Utils;
using Net;

namespace Sim
{
    public sealed class ServerEngine
    {
        private readonly World _world;
        private readonly TickTicker _ticker;

        private readonly ClientCommandBuffer2 _cmdBuffer;

        private readonly List<int> _clients;
        private readonly Dictionary<int, ServerReliableStream> _reliableStreams;

        private const int ReliableMaxOpsBytesPerTick = 8 * 1024;
        private const int ReliableMaxPendingPackets = 128;

        private uint _serverSampleSeq;

        private readonly NetDataWriter _opsWriter;

        private readonly Queue<OutboundPacket> _outbound;

        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;

        public ServerEngine(int tickRateHz, World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _ticker = new TickTicker(tickRateHz);

            _cmdBuffer = new ClientCommandBuffer2();

            _clients = new List<int>(32);
            _reliableStreams = new Dictionary<int, ServerReliableStream>(32);

            _serverSampleSeq = 1;
            _opsWriter = new NetDataWriter();

            _outbound = new Queue<OutboundPacket>(256);
        }

        // -------------------------
        // Network-facing hooks
        // -------------------------

        public void OnClientConnected(int connId)
        {
            if (!_clients.Contains(connId))
                _clients.Add(connId);

            if (!_reliableStreams.ContainsKey(connId))
                _reliableStreams.Add(connId, new ServerReliableStream(ReliableMaxOpsBytesPerTick, ReliableMaxPendingPackets));

            EnqueueWelcome(connId);
            EnqueueBaseline(connId);
            ReplayReliableStream(connId);
        }

        public void OnClientHello(int connId)
        {
            EnqueueWelcome(connId);
            EnqueueBaseline(connId);
            ReplayReliableStream(connId);
        }

        public void OnClientAck(int connId, ClientAckMsg ack)
        {
            if (_reliableStreams.TryGetValue(connId, out ServerReliableStream stream))
                stream.OnAck(ack.LastAckedReliableSeq);
        }

        public void OnClientPing(int connId, PingMsg ping)
        {
            PongMsg pong = new PongMsg(
                pingId: ping.PingId,
                clientTimeMsEcho: ping.ClientTimeMs,
                serverTimeMs: _ticker.ServerTimeMs,
                serverTick: _ticker.CurrentTick);

            byte[] bytes = MsgCodec.EncodePong(pong);
            _outbound.Enqueue(new OutboundPacket(connId, Lane.Reliable, bytes));
        }

        public void EnqueueClientCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            ClientCommand[] commands,
            int commandCount)
        {
            if (commands == null || commandCount <= 0)
                return;

            ClientCommand[] trimmed = Trim(commands, commandCount);

            int scheduledTick;
            bool accepted = _cmdBuffer.EnqueueWithValidation(
                connectionId: connId,
                requestedClientTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: trimmed,
                currentServerTick: _ticker.CurrentTick,
                scheduledTick: out scheduledTick);

            if (!accepted)
                return;
        }

        public bool TryDequeueOutbound(out OutboundPacket packet)
        {
            if (_outbound.Count == 0)
            {
                packet = default(OutboundPacket);
                return false;
            }

            packet = _outbound.Dequeue();
            return true;
        }

        // -------------------------
        // Tick
        // -------------------------

        public void TickOnce()
        {
            _ticker.Advance(1);

            ExecuteCommandsForCurrentTick();

            _cmdBuffer.DropBeforeTick(_ticker.CurrentTick - 16);

            // TODO: authoritative systems here (AI/combat/collisions/etc)

            if ((_ticker.CurrentTick % Protocol.BaselineIntervalTicks) == 0)
                EnqueueBaselineToAll();

            FlushReliableStreams();

            EnqueueSampleOpsToAll();
        }

        // -------------------------
        // Apply commands
        // -------------------------

        private void ExecuteCommandsForCurrentTick()
        {
            int tick = _ticker.CurrentTick;

            foreach (int connId in _cmdBuffer.ConnectionIdsForTick(tick))
            {
                while (_cmdBuffer.TryDequeueForTick(tick, connId, out ClientCommandBuffer2.ScheduledBatch batch))
                {
                    ApplyClientCommands(batch.Commands);
                }
            }
        }

        private void ApplyClientCommands(ClientCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
                return;

            int i = 0;
            while (i < commands.Length)
            {
                ClientCommand c = commands[i];

                if (c.Type == ClientCommandType.MoveBy)
                    _world.TryMoveEntityBy(c.EntityId, c.Dx, c.Dy);

                i++;
            }
        }

        // -------------------------
        // Outbound message builders
        // -------------------------

        private void EnqueueWelcome(int connId)
        {
            byte[] bytes = MsgCodec.EncodeWelcome(_ticker.TickRateHz, _ticker.CurrentTick);
            _outbound.Enqueue(new OutboundPacket(connId, Lane.Reliable, bytes));
        }

        private void EnqueueBaseline(int connId)
        {
            EntityState[] entities = _world.ToSnapshot();
            uint hash = StateHash.ComputeEntitiesHash(entities);

            BaselineMsg msg = new BaselineMsg(_ticker.CurrentTick, hash, entities);
            byte[] bytes = MsgCodec.EncodeBaseline(msg);

            _outbound.Enqueue(new OutboundPacket(connId, Lane.Reliable, bytes));
        }

        private void EnqueueBaselineToAll()
        {
            int k = 0;
            while (k < _clients.Count)
            {
                EnqueueBaseline(_clients[k]);
                k++;
            }
        }

        private void ReplayReliableStream(int connId)
        {
            if (!_reliableStreams.TryGetValue(connId, out ServerReliableStream stream))
                return;

            foreach (byte[] packetBytes in stream.GetUnackedPackets())
            {
                _outbound.Enqueue(new OutboundPacket(connId, Lane.Reliable, packetBytes));
            }
        }

        public void EmitReliableOpsForClient(int connId, ushort opCount, byte[] opsPayload)
        {
            if (!_reliableStreams.TryGetValue(connId, out ServerReliableStream stream))
                return;

            stream.AddOpsForTick(_ticker.CurrentTick, opCount, opsPayload);
        }

        public void EmitReliableOpsForAll(ushort opCount, byte[] opsPayload)
        {
            int k = 0;
            while (k < _clients.Count)
            {
                EmitReliableOpsForClient(_clients[k], opCount, opsPayload);
                k++;
            }
        }

        private void FlushReliableStreams()
        {
            int serverTick = _ticker.CurrentTick;

            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];

                if (_reliableStreams.TryGetValue(connId, out ServerReliableStream stream))
                {
                    uint hash = StateHash.ComputeWorldHash(_world);

                    byte[] packetBytes = stream.FlushToPacketIfAny(serverTick, hash);
                    if (packetBytes != null)
                        _outbound.Enqueue(new OutboundPacket(connId, Lane.Reliable, packetBytes));
                }

                k++;
            }
        }

        private void EnqueueSampleOpsToAll()
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

            byte[] opsBytes = _opsWriter.CopyData();

            ServerOpsMsg msg = new ServerOpsMsg(
                serverTick: _ticker.CurrentTick,
                serverSeq: _serverSampleSeq++,
                stateHash: hash,
                opCount: opCount,
                opsPayload: opsBytes);

            byte[] packetBytes = MsgCodec.EncodeServerOps(Lane.Sample, msg);

            int k = 0;
            while (k < _clients.Count)
            {
                int connId = _clients[k];
                _outbound.Enqueue(new OutboundPacket(connId, Lane.Sample, packetBytes));
                k++;
            }
        }

        private static ClientCommand[] Trim(ClientCommand[] src, int count)
        {
            if (count <= 0)
                return Array.Empty<ClientCommand>();

            if (src.Length == count)
                return src;

            ClientCommand[] dst = new ClientCommand[count];

            int i = 0;
            while (i < count)
            {
                dst[i] = src[i];
                i++;
            }

            return dst;
        }

        public readonly struct OutboundPacket
        {
            public readonly int ConnId;
            public readonly Lane Lane;
            public readonly byte[] Bytes;

            public OutboundPacket(int connId, Lane lane, byte[] bytes)
            {
                ConnId = connId;
                Lane = lane;
                Bytes = bytes;
            }
        }
    }
}
