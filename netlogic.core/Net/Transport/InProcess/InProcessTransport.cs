using System.Collections.Concurrent;

namespace Net
{
    /// <summary>
    /// In-process transport implementation using concurrent queues for client-server communication.
    /// </summary>
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

        /// <summary>
        /// Internal endpoint implementation using concurrent queues.
        /// </summary>
        private sealed class Endpoint(ConcurrentQueue<IMessage> sendQueue, ConcurrentQueue<IMessage> recvQueue) : ITransportEndpoint
        {
            private readonly ConcurrentQueue<IMessage> _send = sendQueue;
            private readonly ConcurrentQueue<IMessage> _recv = recvQueue;

            public void Send(IMessage msg) => _send.Enqueue(msg);

            public bool TryReceive(out IMessage msg) => _recv.TryDequeue(out msg!);
        }
    }
}
