using Net;

namespace Client2.Protocol
{
    // Thin wrappers so GameClient doesn't depend on LiteNetLib types.
    public readonly record struct NetConnected();
    public readonly record struct NetDisconnected(string Reason);

    public readonly record struct NetBaseline(
        int ServerTick,
        uint StateHash,
        EntityState[] Entities);

    public readonly record struct NetTickOps(
        int ServerTick,
        uint StateHash,
        ushort OpCount,
        byte[] OpsPayload,
        Lane Lane);

    public readonly record struct NetAck(uint AckSeq);
    public readonly record struct NetPing(uint PingId, long ClientTimeMs, int ClientTick);
    public readonly record struct NetPong(uint PingId, long ClientTimeMsEcho, double ServerTimeMs, int ServerTick);
}
