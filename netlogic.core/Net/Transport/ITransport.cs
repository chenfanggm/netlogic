namespace com.aqua.netlogic.net.transport
{
    /// <summary>
    /// Non-blocking network endpoint for sending and receiving messages.
    /// </summary>
    public interface ITransportEndpoint
    {
        // Non-blocking: returns false if no message available
        bool TryReceive(out IMessage msg);

        // Non-blocking send
        void Send(IMessage msg);
    }

    /// <summary>
    /// A pair of endpoints connecting client and server for bidirectional communication.
    /// </summary>
    public interface ITransportLink
    {
        ITransportEndpoint ClientEnd { get; }
        ITransportEndpoint ServerEnd { get; }
    }
}
