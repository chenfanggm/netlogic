using System;
using Game;
using LiteNetLib.Utils;
using Net;

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

        private readonly NetDataWriter _opsWriter;

        private readonly SnapshotRingBuffer _snapshots;
        private readonly RenderInterpolator _interpolator;

        private int _lastReceivedServerTick;
        private int _renderDelayTicks;

        public bool IsConnected => _transport.IsConnected;

        public GameClient(IClientTransport transport, int tickRateHz)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _tickRateHz = tickRateHz;

            _opsWriter = new NetDataWriter();

            _snapshots = new SnapshotRingBuffer(capacity: 256);
            _interpolator = new RenderInterpolator();

            _lastReceivedServerTick = -1;
            _renderDelayTicks = 2;
        }

        public void Start()
        {
            _transport.Start();
        }

        public void Connect(string host, int port)
        {
            _transport.Connect(host, port);
        }

        public void Poll()
        {
            _transport.Poll();

            NetPacket packet;
            while (_transport.TryReceive(out packet))
            {
                ServerOpsMsg msg;
                bool ok = MsgCodec.TryDecodeServerOps(packet.Payload, out msg);
                if (!ok)
                    continue;

                if (msg.Lane == Lane.Reliable)
                {
                    ApplyReliableOps(msg);
                }
                else
                {
                    ApplySampleOps(msg);
                }
            }
        }

        public void SetRenderDelayTicks(int renderDelayTicks)
        {
            _renderDelayTicks = renderDelayTicks;
        }

        public void SendMoveBy(int clientTick, int entityId, int dx, int dy)
        {
            if (!IsConnected)
                return;

            _opsWriter.Reset();
            ushort opCount = 0;

            OpsWriter.WriteMoveBy(_opsWriter, entityId, dx, dy);
            opCount++;

            ClientOpsMsg msg = new ClientOpsMsg(clientTick, opCount, new ArraySegment<byte>(_opsWriter.Data, 0, _opsWriter.Length));
            byte[] bytes = MsgCodec.EncodeClientOps(msg);

            _transport.Send(Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        public EntityState[] GetRenderEntities()
        {
            // Estimate render tick behind server tick
            int serverTick = _lastReceivedServerTick;
            if (serverTick < 0)
                return Array.Empty<EntityState>();

            double renderTick = serverTick - _renderDelayTicks;

            SnapshotMsg a;
            SnapshotMsg b;
            double alpha;

            bool ok = _snapshots.TryGetInterpolationPair(renderTick, out a, out b, out alpha);
            if (!ok)
                return Array.Empty<EntityState>();

            return _interpolator.Interpolate(a, b, alpha);
        }

        private void ApplyReliableOps(ServerOpsMsg msg)
        {
            // Reliable lane: container ops, discrete events
            // For demo, we don't emit reliable server ops yet.
            // Hook is here to keep architecture correct.

            // If you add reliable server ops, parse payload like sample:
            // NetDataReader r = new NetDataReader(msg.OpsPayload.Array, msg.OpsPayload.Offset, msg.OpsPayload.Count);
        }

        private void ApplySampleOps(ServerOpsMsg msg)
        {
            if (msg.Tick <= _lastReceivedServerTick)
            {
                // stale sample, drop
                return;
            }

            _lastReceivedServerTick = msg.Tick;

            // Parse sample ops into a SnapshotMsg and feed interpolation buffer
            NetDataReader r = new NetDataReader(msg.OpsPayload.Array, msg.OpsPayload.Offset, msg.OpsPayload.Count);

            // This demo builds entity list from PositionAt ops.
            // For production: you likely maintain authoritative entity map and build snapshots from it.
            EntityState[] entities = new EntityState[msg.OpCount];

            int i = 0;
            while (i < msg.OpCount)
            {
                OpType t = OpsReader.ReadOpType(r);

                if (t == OpType.PositionAt)
                {
                    int entityId = r.GetInt();
                    int x = r.GetInt();
                    int y = r.GetInt();

                    entities[i] = new EntityState(entityId, x, y, 0);
                }
                else
                {
                    // Unknown sample op; stop parsing
                    break;
                }

                i++;
            }

            SnapshotMsg snap = new SnapshotMsg(msg.Tick, entities);
            _snapshots.Add(snap);
        }
    }
}
