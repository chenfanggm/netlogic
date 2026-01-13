using System;

namespace Net
{
    /// <summary>
    /// Client-side transport abstraction for connecting and sending/receiving packets.
    /// </summary>
    public interface IClientTransport : IDisposable
    {
        void Start();
        void Connect(string host, int port);
        void Poll();

        bool IsConnected { get; }
        bool TryReceive(out NetPacket packet);

        void Send(Lane lane, ArraySegment<byte> payload);
    }
}
