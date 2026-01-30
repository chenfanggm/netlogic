using System;
using LiteNetLib.Utils;
using com.aqua.netlogic.sim.clientengine;
using com.aqua.netlogic.sim.clientengine.protocol;
using com.aqua.netlogic.net;

namespace com.aqua.netlogic.sim.networkclient
{
    /// <summary>
    /// NetworkClient = real packet client endpoint.
    /// - Owns Net.IClientTransport
    /// - Owns ClientEngine (ClientModel reconstruction)
    ///
    /// Responsibilities:
    /// - Send Hello once connected
    /// - Decode inbound packets (Welcome/Pong/Baseline/ServerOps)
    /// - Convert wire messages -> snapshot/RepOps via ClientMessageDecoder
    /// - Apply snapshot/RepOps into ClientEngine
    /// - Send ClientAck for reliable ServerOps stream
    /// - Encode client commands into ClientOpsMsg and send to server
    /// </summary>
    public sealed class NetworkClient : IDisposable
    {
        private readonly com.aqua.netlogic.net.transport.IClientTransport _transport;

        public ClientEngine Engine { get; }
        public ClientModel Model => Engine.Model;

        private readonly int _clientTickRateHz;
        private bool _sentHello;
        private bool _wasConnected;
        private bool _hasBaseline;

        private uint _clientCmdSeq = 1;
        private uint _lastAckedReliableSeq = 0;

        private readonly NetDataWriter _cmdOpsWriter = new NetDataWriter();
        private readonly ClientMessageDecoder _decoder = new ClientMessageDecoder();

        public NetworkClient(com.aqua.netlogic.net.transport.IClientTransport transport, int clientTickRateHz = 60, ClientEngine? engine = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (clientTickRateHz <= 0)
                throw new ArgumentOutOfRangeException(nameof(clientTickRateHz));

            _clientTickRateHz = clientTickRateHz;
            Engine = engine ?? new ClientEngine();

            _sentHello = false;
            _wasConnected = false;
            _hasBaseline = false;
        }

        public void Start() => _transport.Start();

        public void Connect(string host, int port) => _transport.Connect(host, port);

        public bool IsConnected => _transport.IsConnected;

        public void Poll()
        {
            _transport.Poll();

            bool connected = _transport.IsConnected;
            if (!connected && _wasConnected)
            {
                // connection dropped; reset session state
                _sentHello = false;
                _hasBaseline = false;
                _lastAckedReliableSeq = 0;
            }
            _wasConnected = connected;

            // Send Hello once when connected.
            if (connected && !_sentHello)
            {
                byte[] helloBytes = MsgCodec.EncodeHello(_clientTickRateHz);
                _transport.Send(Lane.Reliable, new ArraySegment<byte>(helloBytes, 0, helloBytes.Length));
                _sentHello = true;
            }

            while (_transport.TryReceive(out NetPacket packet))
            {
                if (packet.Lane == Lane.Reliable)
                {
                    if (MsgCodec.TryDecodeWelcome(packet.Payload, out Welcome _))
                        continue;

                    if (MsgCodec.TryDecodePong(packet.Payload, out PongMsg _))
                        continue;

                    if (MsgCodec.TryDecodeBaseline(packet.Payload, out BaselineMsg baseline))
                    {
                        com.aqua.netlogic.sim.game.snapshot.GameSnapshot snap =
                            _decoder.DecodeBaselineToSnapshot(baseline);
                        Engine.ApplyBaselineSnapshot(snap);
                        _hasBaseline = true;
                        continue;
                    }

                    if (MsgCodec.TryDecodeServerOps(packet.Payload, out ServerOpsMsg relOps))
                    {
                        if (!_hasBaseline)
                            continue;

                        com.aqua.netlogic.sim.replication.ReplicationUpdate update =
                            _decoder.DecodeServerOpsToUpdate(relOps, isReliableLane: true);
                        Engine.ApplyReplicationUpdate(update);

                        // Ack reliable ops stream so server replay window works.
                        if (relOps.ServerSeq > _lastAckedReliableSeq)
                        {
                            _lastAckedReliableSeq = relOps.ServerSeq;
                            ClientAckMsg ack = new ClientAckMsg(_lastAckedReliableSeq);
                            byte[] ackBytes = MsgCodec.EncodeClientAck(ack);
                            _transport.Send(Lane.Reliable, new ArraySegment<byte>(ackBytes, 0, ackBytes.Length));
                        }

                        continue;
                    }

                    // Unknown reliable msg: ignore
                    continue;
                }

                // Unreliable lane: normally ServerOps heartbeat/position snapshots
                if (packet.Lane == Lane.Unreliable)
                {
                    if (MsgCodec.TryDecodeServerOps(packet.Payload, out ServerOpsMsg unrelOps))
                    {
                        if (!_hasBaseline)
                            continue;

                        com.aqua.netlogic.sim.replication.ReplicationUpdate update =
                            _decoder.DecodeServerOpsToUpdate(unrelOps, isReliableLane: false);
                        Engine.ApplyReplicationUpdate(update);
                    }
                }
            }
        }

        // -------------------------
        // Outbound commands
        // -------------------------

        public void SendMoveBy(int entityId, int dx, int dy)
        {
            ClientCommand[] cmds = [ClientCommand.MoveBy(entityId, dx, dy)];
            SendCommands(cmds);
        }

        public void SendFlowFire(byte trigger, int param0)
        {
            ClientCommand[] cmds = [ClientCommand.FlowFire(trigger, param0)];
            SendCommands(cmds);
        }

        public void SendCommands(ReadOnlySpan<ClientCommand> cmds)
        {
            if (!_transport.IsConnected)
                return;

            if (cmds.Length == 0)
                return;

            _cmdOpsWriter.Reset();
            ushort opCount = 0;

            for (int i = 0; i < cmds.Length; i++)
            {
                ClientCommandCodec.EncodeToOps(_cmdOpsWriter, cmds[i]);
                opCount++;
            }

            byte[] opsPayload = _cmdOpsWriter.CopyData();

            // requested tick policy (simple): last known server tick + 1
            int requestedTick = (Model.LastServerTick <= 0) ? 1 : (Model.LastServerTick + 1);

            ClientOpsMsg msg = new ClientOpsMsg(
                clientTick: requestedTick,
                clientCmdSeq: _clientCmdSeq++,
                opCount: opCount,
                opsPayload: opsPayload);

            byte[] bytes = MsgCodec.EncodeClientOps(msg);
            _transport.Send(Lane.Reliable, new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        public void Disconnect(string reason) => _transport.Disconnect(reason);

        public void Dispose() => _transport.Dispose();
    }
}
