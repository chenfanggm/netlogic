using System;
using Client2.Protocol;
using Net;

namespace Client2.Net
{
    public interface INetworkClient
    {
        bool IsConnected { get; }
        int ConnId { get; }

        // Events (GameClient subscribes)
        event Action<NetConnected>? Connected;
        event Action<NetDisconnected>? Disconnected;

        event Action<NetBaseline>? BaselineReceived;
        event Action<NetTickOps>? TickOpsReceived;

        event Action<NetAck>? AckReceived;
        event Action<NetPing>? PingReceived;
        event Action<NetPong>? PongReceived;

        void Connect(string host, int port);
        void Disconnect();

        /// <summary>Poll transport and fire events. Call from your main loop.</summary>
        void Poll();

        /// <summary>Send already-encoded bytes (protocol decides). NetworkClient stays dumb.</summary>
        void Send(Lane lane, ArraySegment<byte> bytes);
    }
}
