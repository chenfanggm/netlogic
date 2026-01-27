namespace Net
{
    public static class Protocol
    {
        public const ushort Version = ProtocolVersion.Current;

        // OPTIONAL: set this from your build pipeline (git hash -> uint)
        public const uint BuildHash = 0x00000000;

        // Baseline snapshot cadence (server tick count)
        public const int BaselineIntervalTicks = 40;

        // Client catch-up thresholds
        public const int MaxSampleTickGapBeforeSnap = 10;
        public const int DefaultRenderDelayTicks = 2;

        // Ping cadence
        public const int PingIntervalTicks = 20;
    }

    public enum MsgKind : byte
    {
        Hello = 1,
        Welcome = 2,

        ClientOps = 10,      // reliable lane only
        ServerOps = 11,      // can be reliable or sample lane

        Baseline = 12,       // reliable full snapshot

        Ping = 20,           // reliable
        Pong = 21,           // reliable

        ClientAck = 30       // reliable
    }
}
