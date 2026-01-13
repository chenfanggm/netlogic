using System;
using System.Collections.Concurrent;

namespace Net
{
    public sealed class LossyInProcessTransportLink : ITransportLink
    {
        public ITransportEndpoint ClientEnd { get; }
        public ITransportEndpoint ServerEnd { get; }

        public LossyInProcessTransportLink(
            double lossClientToServer,
            double lossServerToClient,
            int randomSeed)
        {
            ConcurrentQueue<IMessage> c2s = new ConcurrentQueue<IMessage>();
            ConcurrentQueue<IMessage> s2c = new ConcurrentQueue<IMessage>();

            Random rng = new Random(randomSeed);

            ClientEnd = new LossyEndpoint(sendQueue: c2s, recvQueue: s2c, rng: rng, lossRate: lossClientToServer);
            ServerEnd = new LossyEndpoint(sendQueue: s2c, recvQueue: c2s, rng: rng, lossRate: lossServerToClient);
        }

        private sealed class LossyEndpoint : ITransportEndpoint
        {
            private readonly ConcurrentQueue<IMessage> _send;
            private readonly ConcurrentQueue<IMessage> _recv;
            private readonly Random _rng;
            private readonly double _lossRate;

            public LossyEndpoint(
                ConcurrentQueue<IMessage> sendQueue,
                ConcurrentQueue<IMessage> recvQueue,
                Random rng,
                double lossRate)
            {
                _send = sendQueue;
                _recv = recvQueue;
                _rng = rng;
                _lossRate = lossRate;
            }

            public void Send(IMessage msg)
            {
                // Drop with probability lossRate
                double r = _rng.NextDouble();
                if (r < _lossRate)
                    return;

                _send.Enqueue(msg);
            }

            public bool TryReceive(out IMessage msg)
            {
                return _recv.TryDequeue(out msg!);
            }
        }
    }
}
