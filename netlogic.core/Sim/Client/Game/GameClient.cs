using System;
using Client.Net;
using Client.Protocol;

namespace Client.Game
{
    /// <summary>
    /// GameClient = applies decoded messages into ClientModel.
    /// Rendering reads Model.
    /// </summary>
    public sealed class GameClient
    {
        public ClientModel Model { get; } = new ClientModel();

        private readonly NetworkClient _net;


        public GameClient(NetworkClient net)
        {
            _net = net ?? throw new ArgumentNullException(nameof(net));

            _net.OnServerSnapshot += OnServerSnapshot;
        }

        public void Poll() => _net.Poll();

        // -------------------------
        // Input sending API (example)
        // -------------------------

        public void SendMoveBy(int entityId, int dx, int dy)
        {
            ClientCommand cmd = ClientCommand.MoveBy(entityId, dx, dy);
            _net.SendClientCommand(cmd);
        }

        public void SendFlowFire(byte trigger, int param0)
        {
            ClientCommand cmd = ClientCommand.FlowFire(trigger, param0);
            _net.SendClientCommand(cmd);
        }

        // -------------------------
        // Receive/apply
        // -------------------------

        private void OnServerSnapshot(ServerSnapshot snapshot)
        {
            Model.ApplySnapshot(snapshot);
        }
    }
}
