using System;
using System.Collections.Concurrent;

namespace Net
{
    /// <summary>
    /// In-process transport pair for fast development/testing without real network sockets.
    /// </summary>
    public sealed class InProcessTransportPair
    {
        public readonly InProcessServerTransport Server;
        public readonly InProcessClientTransport Client;

        public InProcessTransportPair()
        {
            ConcurrentQueue<int> connected = new ConcurrentQueue<int>();

            ConcurrentQueue<NetPacket> c2s = new ConcurrentQueue<NetPacket>();
            ConcurrentQueue<NetPacket> s2c = new ConcurrentQueue<NetPacket>();

            Server = new InProcessServerTransport(connected, c2s, s2c);
            Client = new InProcessClientTransport(connected, c2s, s2c);
        }
    }

    /// <summary>
    /// In-process server transport implementation using concurrent queues.
    /// </summary>
    public sealed class InProcessServerTransport(
        ConcurrentQueue<int> connected,
        ConcurrentQueue<NetPacket> c2s,
        ConcurrentQueue<NetPacket> s2c) : IServerTransport
    {
        private readonly ConcurrentQueue<int> _connected = connected;
        private readonly ConcurrentQueue<NetPacket> _c2s = c2s;
        private readonly ConcurrentQueue<NetPacket> _s2c = s2c;

        public void Start(int port)
        {
            // no-op
        }

        public void Poll()
        {
            // no-op
        }

        public bool TryDequeueConnected(out int connectionId)
        {
            return _connected.TryDequeue(out connectionId);
        }

        public bool TryReceive(out NetPacket packet)
        {
            return _c2s.TryDequeue(out packet);
        }

        public void Send(int connectionId, Lane lane, ArraySegment<byte> payload)
        {
            NetPacket packet = new NetPacket(connectionId, lane, payload);
            _s2c.Enqueue(packet);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// In-process client transport implementation using concurrent queues.
    /// </summary>
    public sealed class InProcessClientTransport(
        ConcurrentQueue<int> connected,
        ConcurrentQueue<NetPacket> c2s,
        ConcurrentQueue<NetPacket> s2c) : IClientTransport
    {
        private readonly ConcurrentQueue<int> _connected = connected;
        private readonly ConcurrentQueue<NetPacket> _c2s = c2s;
        private readonly ConcurrentQueue<NetPacket> _s2c = s2c;

        private int _connectionId = 1;
        private bool _isConnected = false;

        public void Start()
        {
            // no-op
        }

        public void Connect(string host, int port)
        {
            // Immediately "connect"
            _isConnected = true;
            _connected.Enqueue(_connectionId);
        }

        public void Poll()
        {
            // no-op
        }

        public bool IsConnected
        {
            get { return _isConnected; }
        }

        public bool TryReceive(out NetPacket packet)
        {
            return _s2c.TryDequeue(out packet);
        }

        public void Send(Lane lane, ArraySegment<byte> payload)
        {
            NetPacket packet = new NetPacket(_connectionId, lane, payload);
            _c2s.Enqueue(packet);
        }

        public void Dispose()
        {
        }
    }
}
