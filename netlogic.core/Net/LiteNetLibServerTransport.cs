using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Net
{
    /// <summary>
    /// LiteNetLib-based server transport implementation for real UDP networking.
    /// </summary>
    public sealed class LiteNetLibServerTransport : IServerTransport
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _server;

        private readonly ConcurrentQueue<int> _connected;
        private readonly ConcurrentQueue<NetPacket> _received;

        private readonly Dictionary<int, NetPeer> _peersByConnId;
        private int _nextConnId;

        private readonly NetDataWriter _sendWriter;

        public LiteNetLibServerTransport()
        {
            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener);

            _connected = new ConcurrentQueue<int>();
            _received = new ConcurrentQueue<NetPacket>();

            _peersByConnId = new Dictionary<int, NetPeer>(64);
            _nextConnId = 1;

            _sendWriter = new NetDataWriter();

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
        }

        public void Start(int port)
        {
            _server.Start(port);
        }

        public void Poll()
        {
            _server.PollEvents();
        }

        public bool TryDequeueConnected(out int connectionId)
        {
            return _connected.TryDequeue(out connectionId);
        }

        public bool TryReceive(out NetPacket packet)
        {
            return _received.TryDequeue(out packet);
        }

        public void Send(int connectionId, Lane lane, ArraySegment<byte> payload)
        {
            NetPeer? peer;
            if (!_peersByConnId.TryGetValue(connectionId, out peer) || peer == null)
                return;

            DeliveryMethod method = lane == Lane.Reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

            _sendWriter.Reset();
            _sendWriter.Put((byte)lane);
            _sendWriter.Put(payload.Array, payload.Offset, payload.Count);

            peer.Send(_sendWriter, method);
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            request.AcceptIfKey("game_key");
        }

        private void OnPeerConnected(NetPeer peer)
        {
            int connId = _nextConnId++;
            peer.Tag = connId;
            _peersByConnId[connId] = peer;
            _connected.Enqueue(connId);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            object tag = peer.Tag;
            if (tag is int)
            {
                int connId = (int)tag;
                _peersByConnId.Remove(connId);
            }
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            object tag = peer.Tag;
            if (!(tag is int))
            {
                reader.Recycle();
                return;
            }

            int connId = (int)tag;

            if (reader.AvailableBytes < 1)
            {
                reader.Recycle();
                return;
            }

            Lane lane = (Lane)reader.GetByte();
            byte[] payload = reader.GetRemainingBytes();

            NetPacket packet = new NetPacket(connId, lane, new ArraySegment<byte>(payload, 0, payload.Length));
            _received.Enqueue(packet);

            reader.Recycle();
        }

        public void Dispose()
        {
            _server.Stop();
        }
    }
}
