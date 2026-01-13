namespace Net
{
    public interface ITransportEndpoint
    {
        // Non-blocking: returns false if no message available
        bool TryReceive(out IMessage msg);

        // Non-blocking send
        void Send(IMessage msg);
    }

    // A pair of endpoints: client end and server end
    public interface ITransportLink
    {
        ITransportEndpoint ClientEnd { get; }
        ITransportEndpoint ServerEnd { get; }
    }
}
