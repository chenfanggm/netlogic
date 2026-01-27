using System;
using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Net
{
    /// <summary>
    /// LiteNetLib-based client transport implementation for real UDP networking.
    /// </summary>
    public sealed class LiteNetLibClientTransport : IClientTransport
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _client;

        private readonly ConcurrentQueue<NetPacket> _received;

        private readonly NetDataWriter _sendWriter;

        private NetPeer? _serverPeer;

        public LiteNetLibClientTransport()
        {
            _listener = new EventBasedNetListener();
            _client = new NetManager(_listener);

            _received = new ConcurrentQueue<NetPacket>();
            _sendWriter = new NetDataWriter();

            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
        }

        public void Start()
        {
            _client.Start();
        }

        public void Connect(string host, int port)
        {
            _serverPeer = _client.Connect(host, port, "game_key");
        }

        public void Poll()
        {
            _client.PollEvents();
        }

        public bool IsConnected
        {
            get { return _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected; }
        }

        public bool TryReceive(out NetPacket packet)
        {
            return _received.TryDequeue(out packet);
        }

        public void Send(Lane lane, ArraySegment<byte> payload)
        {
            if (_serverPeer == null)
                return;

            DeliveryMethod method = lane == Lane.Reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

            _sendWriter.Reset();
            _sendWriter.Put((byte)lane);
            _sendWriter.Put(payload.Array, payload.Offset, payload.Count);

            _serverPeer.Send(_sendWriter, method);
        }

        public void Disconnect(string reason)
        {
            if (_serverPeer == null)
                return;

            Console.WriteLine($"[Net] Client disconnecting. {reason}");
            _serverPeer.Disconnect();
            _serverPeer = null;
        }

        private void OnPeerConnected(NetPeer peer)
        {
            _serverPeer = peer;
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            if (_serverPeer == peer)
                _serverPeer = null;
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes < 1)
            {
                reader.Recycle();
                return;
            }

            Lane lane = (Lane)reader.GetByte();
            byte[] payload = reader.GetRemainingBytes();

            NetPacket packet = new NetPacket(1, lane, new ArraySegment<byte>(payload, 0, payload.Length));
            _received.Enqueue(packet);

            reader.Recycle();
        }

        public void Dispose()
        {
            _client.Stop();
        }
    }
}
