using System;
using Net;

namespace Client2.Net
{
    /// <summary>
    /// In-process "network feed":
    /// lets a harness push Baseline/ServerOps directly into GameClient2,
    /// while still respecting the separation (GameClient2 doesn't touch GameEngine).
    /// </summary>
    public sealed class InProcessNetFeed
    {
        public event Action<BaselineMsg>? BaselineReceived;
        public event Action<ServerOpsMsg, Lane>? ServerOpsReceived;

        public void PushBaseline(BaselineMsg baseline) => BaselineReceived?.Invoke(baseline);

        public void PushOps(ServerOpsMsg ops, Lane lane) => ServerOpsReceived?.Invoke(ops, lane);
    }
}
