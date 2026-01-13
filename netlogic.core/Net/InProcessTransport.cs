using System.Collections.Concurrent;

namespace Net
{
    public sealed class InProcessTransportLink : ITransportLink
    {
        public ITransportEndpoint ClientEnd { get; }
        public ITransportEndpoint ServerEnd { get; }

        public InProcessTransportLink()
        {
            ConcurrentQueue<IMessage> c2s = new();
            ConcurrentQueue<IMessage> s2c = new();

            ClientEnd = new Endpoint(sendQueue: c2s, recvQueue: s2c);
            ServerEnd = new Endpoint(sendQueue: s2c, recvQueue: c2s);
        }

        private sealed class Endpoint : ITransportEndpoint
        {
            private readonly ConcurrentQueue<IMessage> _send;
            private readonly ConcurrentQueue<IMessage> _recv;

            public Endpoint(ConcurrentQueue<IMessage> sendQueue, ConcurrentQueue<IMessage> recvQueue)
            {
                _send = sendQueue;
                _recv = recvQueue;
            }

            public void Send(IMessage msg) => _send.Enqueue(msg);

            public bool TryReceive(out IMessage msg) => _recv.TryDequeue(out msg!);
        }
    }
}
