using System;
using Game;
using Net;

namespace Sim
{
    public sealed class GameServer(IServerTransport transport, int tickRateHz, World world)
    {
        private readonly IServerTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        private readonly ServerEngine _engine = new ServerEngine(tickRateHz, world);

        private readonly ClientOpsMsgToClientCommandConverter _converter = new ClientOpsMsgToClientCommandConverter(initialCapacity: 32);

        public int CurrentServerTick => _engine.CurrentServerTick;
        public int TickRateHz => _engine.TickRateHz;

        public void Start(int port)
        {
            _transport.Start(port);
        }

        public void Poll()
        {
            _transport.Poll();

            ProcessNewConnections();
            ProcessPackets();
            FlushOutbound();
        }

        public void TickOnce()
        {
            _engine.TickOnce();
            FlushOutbound();
        }

        private void ProcessNewConnections()
        {
            while (_transport.TryDequeueConnected(out int connId))
            {
                _engine.OnClientConnected(connId);
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
                    _engine.OnClientHello(packet.ConnId);
                    continue;
                }

                if (MsgCodec.TryDecodePing(packet.Payload, out PingMsg ping))
                {
                    _engine.OnClientPing(packet.ConnId, ping);
                    continue;
                }

                if (MsgCodec.TryDecodeClientAck(packet.Payload, out ClientAckMsg ack))
                {
                    _engine.OnClientAck(packet.ConnId, ack);
                    continue;
                }

                if (MsgCodec.TryDecodeClientOps(packet.Payload, out ClientOpsMsg ops))
                {
                    ClientCommand[] commands = _converter.Convert(ops, out int commandCount);

                    _engine.EnqueueClientCommands(
                        connId: packet.ConnId,
                        requestedClientTick: ops.ClientTick,
                        clientCmdSeq: ops.ClientCmdSeq,
                        commands: commands,
                        commandCount: commandCount);

                    continue;
                }
            }
        }

        private void FlushOutbound()
        {

            while (_engine.TryDequeueOutbound(out ServerEngine.OutboundPacket p))
            {
                byte[] bytes = p.Bytes;
                _transport.Send(p.ConnId, p.Lane, new ArraySegment<byte>(bytes, 0, bytes.Length));
            }
        }
    }
}
