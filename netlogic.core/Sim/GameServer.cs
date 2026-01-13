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
    public sealed class GameServer(IServerTransport transport, int tickRateHz, World world)
    {
        public int CurrentServerTick => _ticker.CurrentTick;
        public int TickRateHz => _ticker.TickRateHz;

        private readonly IServerTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
        private readonly TickTicker _ticker = new TickTicker(tickRateHz);
        private readonly List<int> _clients = new List<int>(32);
        private readonly ClientCommandBuffer _cmdBuffer = new ClientCommandBuffer();

        private uint _serverReliableSeq = 1;
        private uint _serverSampleSeq = 1;

        private readonly NetDataWriter _opsWriter = new NetDataWriter();

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

        public void TickOnce()
        {
            _ticker.Advance(1);

            ExecuteCommandsForCurrentTick();
            _cmdBuffer.DropBeforeTick(_ticker.CurrentTick - 10);

            // TODO: run authoritative systems here (AI, combat, grid collisions, etc.)

            // Baseline snapshots at cadence for recovery & idempotency
            if ((_ticker.CurrentTick % Protocol.BaselineIntervalTicks) == 0)
            {
                SendBaselineToAll();
            }

            // Reliable server ops (container ops/events) - currently empty hook
            SendReliableOpsToAll();

            // Sample server ops (positions) - latest wins
            SendSampleOpsToAll();
        }

        private void ProcessPackets()
        {
            while (_transport.TryReceive(out NetPacket packet))
            {
                // All control and commands are Reliable lane only
                if (packet.Lane != Lane.Reliable)
                    continue;

                if (ProcessHello(packet))
                    continue;

                if (ProcessPing(packet))
                    continue;

                if (ProcessClientOps(packet))
                    continue;
            }
        }

        private void ProcessNewConnections()
        {
            while (_transport.TryDequeueConnected(out int connId))
            {
                if (!_clients.Contains(connId))
                    _clients.Add(connId);

                // Immediately send Welcome + Baseline (join-in-progress ready)
                SendWelcome(connId);
                SendBaseline(connId);
            }
        }

        private bool ProcessHello(NetPacket packet)
        {
            if (!MsgCodec.TryDecodeHello(packet.Payload, out Hello hello))
                return false;

            // Validate protocol/version
            // (We already validate in decoder)
            SendWelcome(packet.ConnId);
            SendBaseline(packet.ConnId);
            return true;
        }

        private bool ProcessPing(NetPacket packet)
        {
            if (!MsgCodec.TryDecodePing(packet.Payload, out PingMsg ping))
                return false;

            PongMsg pong = new PongMsg(
                pingId: ping.PingId,
                clientTimeMsEcho: ping.ClientTimeMs,
                serverTimeMs: _ticker.ServerTimeMs,
                serverTick: _ticker.CurrentTick);

            byte[] pongBytes = MsgCodec.EncodePong(pong);
            _transport.Send(packet.ConnId, Lane.Reliable, new ArraySegment<byte>(pongBytes, 0, pongBytes.Length));
            return true;
        }

        private bool ProcessClientOps(NetPacket packet)
        {
            if (!MsgCodec.TryDecodeClientOps(packet.Payload, out ClientOpsMsg cmd))
                return false;

            // Only enqueue during Poll; execute during TickOnce
            _cmdBuffer.Enqueue(packet.ConnId, cmd);
            return true;
        }

        private void ExecuteCommandsForCurrentTick()
        {
            int tick = _ticker.CurrentTick;

            foreach (int connId in _cmdBuffer.ConnectionIdsForTick(tick))
            {
                while (_cmdBuffer.TryDequeueForTick(tick, connId, out ClientOpsMsg msg))
                {
                    ApplyClientOps(connId, msg);
                }
            }
        }

        private void ApplyClientOps(int connectionId, ClientOpsMsg msg)
        {
            NetDataReader r = new NetDataReader(msg.OpsPayload, 0, msg.OpsPayload.Length);

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
            byte[] bytes = MsgCodec.EncodeWelcome(_ticker.TickRateHz, _ticker.CurrentTick);
            _transport.Send(connId, Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        private void SendBaseline(int connId)
        {
            EntityState[] entities = _world.ToSnapshot();
            uint hash = StateHash.ComputeEntitiesHash(entities);

            BaselineMsg msg = new BaselineMsg(_ticker.CurrentTick, hash, entities);
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

            byte[] opsBytes = _opsWriter.CopyData();

            ServerOpsMsg msg = new ServerOpsMsg(
                serverTick: _ticker.CurrentTick,
                serverSeq: _serverReliableSeq++,
                stateHash: hash,
                opCount: opCount,
                opsPayload: opsBytes);

            byte[] packet = MsgCodec.EncodeServerOps(Lane.Reliable, msg);

            ArraySegment<byte> payload = new ArraySegment<byte>(packet, 0, packet.Length);

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

            byte[] opsBytes = _opsWriter.CopyData();

            ServerOpsMsg msg = new ServerOpsMsg(
                serverTick: _ticker.CurrentTick,
                serverSeq: _serverSampleSeq++,
                stateHash: hash,
                opCount: opCount,
                opsPayload: opsBytes);

            byte[] packet = MsgCodec.EncodeServerOps(Lane.Sample, msg);

            ArraySegment<byte> payload = new ArraySegment<byte>(packet, 0, packet.Length);

            int k = 0;
            while (k < _clients.Count)
            {
                _transport.Send(_clients[k], Lane.Sample, payload);
                k++;
            }
        }
    }
}
