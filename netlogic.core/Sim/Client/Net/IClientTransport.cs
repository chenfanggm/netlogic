using System;

namespace Client.Net
{
    [global::System.Obsolete("Legacy object/message transport. Use Net.IClientTransport (packet transport) + MsgCodec + Baseline/Ops.")]
    public interface IClientTransport : IDisposable
    {
        // Transport emits decoded messages as objects (e.g., ServerSnapshot).
        event Action<object>? OnReceive;

        // Send typed messages client->server (e.g., ClientCommand).
        void Send<T>(T msg) where T : class;

        // Pump network events; deterministic harness calls this each tick.
        void Poll();
    }
}
