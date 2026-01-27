using System;
using System.Net;
using System.Net.Sockets;
using Client2.Protocol;
using LiteNetLib;
using LiteNetLib.Utils;
using Net;

namespace Client2.Net
{
    /// <summary>
    /// Transportation only.
    /// - Owns LiteNetLib peer
    /// - Decodes packets into protocol messages using MsgCodec
    /// - Emits events for GameClient
    /// </summary>
    public sealed class LiteNetNetworkClient : INetworkClient, INetEventListener
    {
        public bool IsConnected => _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected;
        public int ConnId => _serverPeer?.Id ?? -1;

        public event Action<NetConnected>? Connected;
        public event Action<NetDisconnected>? Disconnected;

        public event Action<NetBaseline>? BaselineReceived;
        public event Action<NetTickOps>? TickOpsReceived;

        public event Action<NetAck>? AckReceived;
        public event Action<NetPing>? PingReceived;
        public event Action<NetPong>? PongReceived;

        private readonly NetManager _net;
        private NetPeer? _serverPeer;

        public LiteNetNetworkClient()
        {
            _net = new NetManager(this)
            {
                AutoRecycle = true
            };
        }

        public void Connect(string host, int port)
        {
            _net.Start();
            _serverPeer = _net.Connect(host, port, "netlogic");
        }

        public void Disconnect()
        {
            _serverPeer?.Disconnect();
            _net.Stop();
            _serverPeer = null;
        }

        public void Poll()
        {
            _net.PollEvents();
        }

        public void Send(Lane lane, ArraySegment<byte> bytes)
        {
            if (_serverPeer == null) return;

            DeliveryMethod dm = lane == Lane.Reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
            _serverPeer.Send(bytes.Array!, bytes.Offset, bytes.Count, dm);
        }

        // ---- LiteNetLib callbacks ----

        public void OnPeerConnected(NetPeer peer)
        {
            _serverPeer = peer;
            Connected?.Invoke(new NetConnected());
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Disconnected?.Invoke(new NetDisconnected(disconnectInfo.Reason.ToString()));
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            // Existing packets are MsgCodec encoded.
            byte[] payload = reader.GetRemainingBytes();

            if (MsgCodec.TryDecodeBaseline(payload, out BaselineMsg baseline))
            {
                BaselineReceived?.Invoke(new NetBaseline(
                    ServerTick: baseline.ServerTick,
                    StateHash: baseline.StateHash,
                    Entities: baseline.Entities));
                return;
            }

            if (MsgCodec.TryDecodeServerOps(payload, out ServerOpsMsg ops))
            {
                Lane lane = deliveryMethod == DeliveryMethod.ReliableOrdered ? Lane.Reliable : Lane.Sample;
                TickOpsReceived?.Invoke(new NetTickOps(
                    ServerTick: ops.ServerTick,
                    StateHash: ops.StateHash,
                    OpCount: ops.OpCount,
                    OpsPayload: ops.OpsPayload,
                    Lane: lane));
                return;
            }

            if (MsgCodec.TryDecodePing(payload, out PingMsg ping))
            {
                PingReceived?.Invoke(new NetPing(ping.PingId, ping.ClientTimeMs, ping.ClientTick));
                return;
            }

            if (MsgCodec.TryDecodePong(payload, out PongMsg pong))
            {
                PongReceived?.Invoke(new NetPong(pong.PingId, pong.ClientTimeMsEcho, pong.ServerTimeMs, pong.ServerTick));
                return;
            }

            // Ack is client -> server in current protocol; keep hook for future.
            if (MsgCodec.TryDecodeClientAck(payload, out ClientAckMsg ack))
            {
                AckReceived?.Invoke(new NetAck(ack.LastAckedReliableSeq));
                return;
            }
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public void OnConnectionRequest(ConnectionRequest request) => request.AcceptIfKey("netlogic");
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    }
}
