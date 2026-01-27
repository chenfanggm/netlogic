using System;
using Net;

namespace Client2.Net
{
    /// <summary>
    /// NetworkClient2 = transport + packet decode ONLY.
    /// Emits decoded message callbacks. No game state here.
    /// </summary>
    public sealed class NetworkClient2
    {
        private readonly IClientTransport _transport;

        public bool IsConnected => _transport.IsConnected;

        public event Action? Connected;
        public event Action<string>? Disconnected;

        public event Action<BaselineMsg>? BaselineReceived;
        public event Action<ServerOpsMsg, Lane>? ServerOpsReceived;

        public event Action<PongMsg>? PongReceived;
        public event Action<Welcome>? WelcomeReceived;

        private bool _helloSent;
        private readonly int _clientTickRateHz;

        public NetworkClient2(IClientTransport transport, int clientTickRateHz)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _clientTickRateHz = clientTickRateHz;
            _helloSent = false;
        }

        public void Start() => _transport.Start();

        public void Connect(string host, int port)
        {
            _transport.Connect(host, port);
            _helloSent = false;
        }

        public void Poll()
        {
            _transport.Poll();

            if (IsConnected && !_helloSent)
            {
                byte[] hello = MsgCodec.EncodeHello(_clientTickRateHz);
                _transport.Send(Lane.Reliable, new ArraySegment<byte>(hello, 0, hello.Length));
                _helloSent = true;
                Connected?.Invoke();
            }

            while (_transport.TryReceive(out NetPacket packet))
            {
                if (packet.Lane == Lane.Reliable)
                {
                    if (MsgCodec.TryDecodeWelcome(packet.Payload, out Welcome welcome))
                    {
                        WelcomeReceived?.Invoke(welcome);
                        continue;
                    }

                    if (MsgCodec.TryDecodePong(packet.Payload, out PongMsg pong))
                    {
                        PongReceived?.Invoke(pong);
                        continue;
                    }

                    if (MsgCodec.TryDecodeBaseline(packet.Payload, out BaselineMsg baseline))
                    {
                        BaselineReceived?.Invoke(baseline);
                        continue;
                    }

                    if (MsgCodec.TryDecodeServerOps(packet.Payload, out ServerOpsMsg relOps))
                    {
                        ServerOpsReceived?.Invoke(relOps, Lane.Reliable);
                        continue;
                    }
                }

                if (packet.Lane == Lane.Sample)
                {
                    if (MsgCodec.TryDecodeServerOps(packet.Payload, out ServerOpsMsg sampOps))
                    {
                        ServerOpsReceived?.Invoke(sampOps, Lane.Sample);
                        continue;
                    }
                }
            }
        }

        public void Send(Lane lane, ArraySegment<byte> payload) => _transport.Send(lane, payload);
    }
}
