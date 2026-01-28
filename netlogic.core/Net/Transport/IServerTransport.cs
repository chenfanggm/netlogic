using System;

namespace com.aqua.netlogic.net.transport
{
    /// <summary>
    /// Server-side transport abstraction for accepting connections and sending/receiving packets.
    /// </summary>
    public interface IServerTransport : IDisposable
    {
        void Start(int port);
        void Poll();

        // Accept new connections, provide ConnectionId
        bool TryDequeueConnected(out int connectionId);

        // Receive packets from any client
        bool TryReceive(out NetPacket packet);

        // Send to one connection
        void Send(int connectionId, Lane lane, ArraySegment<byte> payload);

        // Disconnect a client with an optional reason
        void Disconnect(int connectionId, string reason);
    }
}
