namespace Net
{
    public static class ProtocolVersion
    {
        // Bump this whenever you change ANY wire format:
        // - message field order
        // - added/removed fields
        // - serialization changes
        public const ushort Current = 1;
    }
}
