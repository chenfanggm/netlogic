using System;
using System.Collections.Generic;
using Game;
using Net;

namespace Sim
{
    /// <summary>
    /// Network layer server:
    /// - Owns transport
    /// - Decodes NetPacket
    /// - Converts ClientOpsMsg -> ClientCommand list
    /// - Feeds ServerEngine
    /// - Sends ServerEngine outbound packets
    /// </summary>
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

            _converter = new ClientOpsMsgToClientCommandConverter(capacity: 32);
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
            // Engine tick is pure (no sockets)
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
                    _engine.OnClientHello(packet.ConnId, hello);
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
                    List<ClientCommand> cmds = _converter.Convert(ops);

                    _engine.EnqueueClientCommands(
                        connId: packet.ConnId,
                        requestedClientTick: ops.ClientTick,
                        clientCmdSeq: ops.ClientCmdSeq,
                        commands: cmds);

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

        /// <summary>
        /// Helper for gameplay systems to emit per-client reliable ops.
        /// Call this from your authoritative tick systems (not Poll).
        /// </summary>
        public void EmitReliableOpsForClient(int connId, ushort opCount, byte[] opsPayload)
        {
            _engine.EmitReliableOpsForClient(connId, opCount, opsPayload);
        }

        /// <summary>
        /// Helper for gameplay systems to emit broadcast reliable ops.
        /// Call this from your authoritative tick systems (not Poll).
        /// </summary>
        public void EmitReliableOpsForAll(ushort opCount, byte[] opsPayload)
        {
            _engine.EmitReliableOpsForAll(opCount, opsPayload);
        }
    }
}
