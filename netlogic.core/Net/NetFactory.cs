namespace Net
{
    public interface INetFactory
    {
        IServerTransport CreateServerTransport();
        IClientTransport CreateClientTransport();
    }

    public sealed class LiteNetLibNetFactory : INetFactory
    {
        public IServerTransport CreateServerTransport()
        {
            return new LiteNetLibServerTransport();
        }

        public IClientTransport CreateClientTransport()
        {
            return new LiteNetLibClientTransport();
        }
    }

    public sealed class InProcessNetFactory : INetFactory
    {
        private readonly InProcessPacketTransportPair _pair = new InProcessPacketTransportPair();

        public IServerTransport CreateServerTransport()
        {
            return _pair.Server;
        }

        public IClientTransport CreateClientTransport()
        {
            return _pair.Client;
        }
    }

    public static class NetFactory
    {
        public static INetFactory Choose(bool useInProcess)
        {
            if (useInProcess)
            {
                return new InProcessNetFactory();
            }
            else
            {
                return new LiteNetLibNetFactory();
            }
        }
    }
}

