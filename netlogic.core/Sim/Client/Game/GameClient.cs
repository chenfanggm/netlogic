using System;
using Client.Net;
using Client.Protocol;
using Net;

namespace Client.Game
{
    /// <summary>
    /// GameClient = joint layer: optional networking + forwards decoded messages into ClientEngine.
    /// Rendering/UI reads Engine.Model.
    /// </summary>
    public sealed class GameClient
    {
        public ClientEngine Engine { get; }
        public ClientModel Model => Engine.Model;

        private readonly NetworkClient? _net;

        // Local/direct mode (no transport)
        public GameClient()
        {
            Engine = new ClientEngine();
            _net = null;
        }

        // Networked mode
        public GameClient(NetworkClient net)
        {
            Engine = new ClientEngine();
            _net = net ?? throw new ArgumentNullException(nameof(net));
            _net.OnServerSnapshot += OnServerSnapshot;
        }

        public void Poll() => _net?.Poll();

        // -------------------------
        // Input sending API (example)
        // -------------------------

        public void SendMoveBy(int entityId, int dx, int dy)
        {
            if (_net == null) throw new InvalidOperationException("No transport attached.");
            ClientCommand cmd = ClientCommand.MoveBy(entityId, dx, dy);
            _net.SendClientCommand(cmd);
        }

        public void SendFlowFire(byte trigger, int param0)
        {
            if (_net == null) throw new InvalidOperationException("No transport attached.");
            ClientCommand cmd = ClientCommand.FlowFire(trigger, param0);
            _net.SendClientCommand(cmd);
        }

        // -------------------------
        // Direct apply APIs (used by local harness / tests)
        // -------------------------

        public void ApplyBaseline(BaselineMsg baseline) => Engine.ApplyBaseline(baseline);
        public void ApplyServerOps(ServerOpsMsg msg) => Engine.ApplyServerOps(msg);
        public void ApplySnapshot(ServerSnapshot snapshot) => Engine.ApplySnapshot(snapshot);

        // -------------------------
        // Legacy network receive path
        // -------------------------

        private void OnServerSnapshot(ServerSnapshot snapshot)
        {
            Engine.ApplySnapshot(snapshot);
        }
    }
}
