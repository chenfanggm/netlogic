using System;
using System.Collections.Concurrent;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace com.aqua.netlogic.net.transport.litenetlib
{
    /// <summary>
    /// LiteNetLib-based server transport implementation for real UDP networking.
    ///
    /// Threading contract (professionalized):
    /// - LiteNetLib callbacks may run on the polling thread, but we do not rely on that.
    /// - All inbound events are enqueued to concurrent queues.
    /// - Peer table is a ConcurrentDictionary so Send/Disconnect are safe even if callbacks fire concurrently.
    /// - Send uses a thread-static NetDataWriter (no shared writer).
    /// </summary>
    public sealed class LiteNetLibServerTransport : IServerTransport
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _server;

        private readonly ConcurrentQueue<int> _connected;
        private readonly ConcurrentQueue<NetPacket> _received;

        // Thread-safe peer table (callbacks mutate; Send/Disconnect read)
        private readonly ConcurrentDictionary<int, NetPeer> _peersByConnId;

        private int _nextConnId;

        [ThreadStatic]
        private static NetDataWriter? _tlsWriter;

        public LiteNetLibServerTransport()
        {
            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener);

            _connected = new ConcurrentQueue<int>();
            _received = new ConcurrentQueue<NetPacket>();

            _peersByConnId = new ConcurrentDictionary<int, NetPeer>();
            _nextConnId = 0;

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
            // PollEvents executes callbacks. We are safe even if this is called from a thread
            // different than Send/Disconnect because we use concurrent structures.
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
            if (!_peersByConnId.TryGetValue(connectionId, out NetPeer? peer) || peer == null)
                return;

            DeliveryMethod method = lane == Lane.Reliable
                ? DeliveryMethod.ReliableOrdered
                : DeliveryMethod.Unreliable;

            NetDataWriter w = _tlsWriter ??= new NetDataWriter();
            w.Reset();

            // Frame: [lane byte][payload...]
            w.Put((byte)lane);
            if (payload.Array != null && payload.Count > 0)
                w.Put(payload.Array, payload.Offset, payload.Count);

            peer.Send(w, method);
        }

        public void Disconnect(int connectionId, string reason)
        {
            if (!_peersByConnId.TryRemove(connectionId, out NetPeer? peer) || peer == null)
                return;

            Console.WriteLine($"[Net] Server disconnecting connId={connectionId}. {reason}");
            peer.Disconnect();
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            request.AcceptIfKey("game_key");
        }

        private void OnPeerConnected(NetPeer peer)
        {
            // Thread-safe connId allocation
            int connId = Interlocked.Increment(ref _nextConnId);
            peer.Tag = connId;

            _peersByConnId[connId] = peer;
            _connected.Enqueue(connId);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            // Remove from peer table
            object? tag = peer.Tag;
            if (tag is int connId)
            {
                _peersByConnId.TryRemove(connId, out _);

                // NOTE: IServerTransport has no "TryDequeueDisconnected".
                // If you later want server-side cleanup callbacks, add a disconnected queue
                // and extend the interface in one controlled change.
            }
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            object? tag = peer.Tag;
            if (tag is not int connId)
            {
                reader.Recycle();
                return;
            }

            if (reader.AvailableBytes < 1)
            {
                reader.Recycle();
                return;
            }

            // Your framing: first byte is lane.
            Lane lane = (Lane)reader.GetByte();
            // lane == Reliable or Unreliable

            // NOTE: GetRemainingBytes allocates a new byte[]; safe for cross-thread queueing.
            // If you later want to reduce allocs, we can pool payload buffers.
            byte[] payload = reader.GetRemainingBytes();

            _received.Enqueue(new NetPacket(connId, lane, new ArraySegment<byte>(payload, 0, payload.Length)));
            reader.Recycle();
        }

        public void Dispose()
        {
            _server.Stop();
        }
    }
}
