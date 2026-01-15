using System;
using System.Collections.Generic;
using Game;
using LiteNetLib.Utils;
using Net;

namespace Sim
{
    public sealed class GameServer(IServerTransport transport, int tickRateHz, World world)
    {
        private readonly IServerTransport _transport = transport;
        private readonly ServerEngine _engine = new ServerEngine(tickRateHz, world);

        private readonly ClientOpsMsgToClientCommandConverter _converter =
            new ClientOpsMsgToClientCommandConverter(initialCapacity: 32);

        private readonly List<int> _clients = new();
        private readonly Dictionary<int, ServerReliableStream> _reliableStreams = new();

        private uint _serverSampleSeq = 1;
        private readonly NetDataWriter _opsWriter = new();

        public void Poll()
        {
            _transport.Poll();
            ProcessConnections();
            ProcessPackets();
        }

        public void TickOnce()
        {
            EngineTickResult tick = _engine.TickOnce();

            ConsumeReliableOps(tick);
            MaybeSendBaseline(tick.ServerTick);
            FlushReliableStreams(tick.ServerTick);
            SendSampleSnapshots(tick);
        }

        private void ProcessConnections()
        {
            while (_transport.TryDequeueConnected(out int connId))
            {
                _clients.Add(connId);
                _reliableStreams[connId] = new ServerReliableStream(8192, 128);
                SendWelcome(connId);
                SendBaseline(connId);
            }
        }

        private void ProcessPackets()
        {
            while (_transport.TryReceive(out NetPacket packet))
            {
                if (packet.Lane != Lane.Reliable)
                    continue;

                if (MsgCodec.TryDecodeClientOps(packet.Payload, out ClientOpsMsg ops))
                {
                    ClientCommand[] commands = _converter.Convert(ops, out int count);
                    _engine.EnqueueClientCommands(
                        packet.ConnId,
                        ops.ClientTick,
                        ops.ClientCmdSeq,
                        commands,
                        count);
                }
            }
        }

        private void ConsumeReliableOps(in EngineTickResult tick)
        {
            foreach (EngineReliableOpBatch b in tick.ReliableOps)
            {
                if (_reliableStreams.TryGetValue(b.ConnId, out ServerReliableStream? stream) && stream != null)
                {
                    byte[] payload = b.OpsPayload ?? Array.Empty<byte>();
                    stream.AddOpsForTick(tick.ServerTick, b.OpCount, payload);
                }
            }
        }

        private void SendSampleSnapshots(in EngineTickResult tick)
        {
            _opsWriter.Reset();
            ushort opCount = 0;

            foreach (SampleEntityPos s in tick.SamplePositions)
            {
                OpsWriter.WritePositionAt(_opsWriter, s.EntityId, s.X, s.Y);
                opCount++;
            }

            if (opCount == 0)
                return;

            ServerOpsMsg msg = new(
                tick.ServerTick,
                _serverSampleSeq++,
                tick.WorldHash,
                opCount,
                _opsWriter.CopyData());

            byte[] bytes = MsgCodec.EncodeServerOps(Lane.Sample, msg);

            foreach (int connId in _clients)
                _transport.Send(connId, Lane.Sample, bytes);
        }

        private void MaybeSendBaseline(int tick)
        {
            if (tick % Protocol.BaselineIntervalTicks != 0)
                return;

            foreach (int connId in _clients)
                SendBaseline(connId);
        }

        private void FlushReliableStreams(int tick)
        {
            foreach (KeyValuePair<int, ServerReliableStream> entry in _reliableStreams)
            {
                int connId = entry.Key;
                ServerReliableStream stream = entry.Value;
                byte[]? bytes = stream.FlushToPacketIfAny(tick, StateHash.ComputeWorldHash(world));
                if (bytes != null)
                    _transport.Send(connId, Lane.Reliable, bytes);
            }
        }

        private void SendWelcome(int connId)
        {
            byte[] bytes = MsgCodec.EncodeWelcome(_engine.TickRateHz, _engine.CurrentServerTick);
            _transport.Send(connId, Lane.Reliable, bytes);
        }

        private void SendBaseline(int connId)
        {
            EntityState[] snapshot = world.ToSnapshot();
            BaselineMsg msg = new(_engine.CurrentServerTick, StateHash.ComputeEntitiesHash(snapshot), snapshot);
            _transport.Send(connId, Lane.Reliable, MsgCodec.EncodeBaseline(msg));
        }
    }
}
