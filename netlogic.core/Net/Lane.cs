namespace Net
{
    /// <summary>
    /// Network packet delivery lane (reliable vs sample/unreliable).
    /// </summary>
    public enum Lane : byte
    {
        Reliable = 1,
        Sample = 2
    }
}
