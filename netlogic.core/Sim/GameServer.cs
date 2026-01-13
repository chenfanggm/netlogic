using System;
using Game;
using Net;

namespace Sim
{
    public sealed class GameServer
    {
        private readonly IServerTransport _transport;
        private readonly ServerEngine _engine;

        private readonly ClientOpsMsgToClientCommandConverter _converter;

        public int CurrentServerTick => _engine.CurrentServerTick;
        public int TickRateHz => _engine.TickRateHz;

        public GameServer(IServerTransport transport, int tickRateHz, World world)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _engine = new ServerEngine(tickRateHz, world);

            _converter = new ClientOpsMsgToClientCommandConverter(initialCapacity: 32);
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

                Hello hello;
                if (MsgCodec.TryDecodeHello(packet.Payload, out hello))
                {
                    _engine.OnClientHello(packet.ConnId);
                    continue;
                }

                PingMsg ping;
                if (MsgCodec.TryDecodePing(packet.Payload, out ping))
                {
                    _engine.OnClientPing(packet.ConnId, ping);
                    continue;
                }

                ClientAckMsg ack;
                if (MsgCodec.TryDecodeClientAck(packet.Payload, out ack))
                {
                    _engine.OnClientAck(packet.ConnId, ack);
                    continue;
                }

                ClientOpsMsg ops;
                if (MsgCodec.TryDecodeClientOps(packet.Payload, out ops))
                {
                    int commandCount;
                    ClientCommand[] commands = _converter.Convert(ops, out commandCount);

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
            ServerEngine.OutboundPacket p;

            while (_engine.TryDequeueOutbound(out p))
            {
                byte[] bytes = p.Bytes;
                _transport.Send(p.ConnId, p.Lane, new ArraySegment<byte>(bytes, 0, bytes.Length));
            }
        }
    }
}
