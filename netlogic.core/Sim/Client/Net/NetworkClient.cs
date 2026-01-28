using System;
using Client.Game;
using Client.Protocol;
using Net;

namespace Client.Net
{
    /// <summary>
    /// NetworkClient = client endpoint (transport/session + forwards server updates into ClientEngine).
    ///
    /// Responsibilities:
    /// - Own a ClientEngine (ClientModel reconstruction)
    /// - Send client commands via transport
    /// - Receive server messages via transport and apply to ClientEngine
    ///
    /// NOTE:
    /// - Today this supports legacy ServerSnapshot (object transport harness).
    /// - It also supports BaselineMsg + ServerOpsMsg if the transport delivers them (future-proof).
    /// - Reliability/replay/ack is still out-of-scope here unless you add it later.
    /// </summary>
    public sealed class NetworkClient
    {
        private readonly IClientTransport _transport;

        public ClientEngine Engine { get; }
        public ClientModel Model => Engine.Model;

        public NetworkClient(IClientTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.OnReceive += OnReceive;

            Engine = new ClientEngine();
        }

        public NetworkClient(IClientTransport transport, ClientEngine engine)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.OnReceive += OnReceive;

            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        // -------------------------
        // Outbound
        // -------------------------

        public void SendClientCommand(ClientCommand cmd)
        {
            _transport.Send(cmd);
        }

        // Convenience helpers (keeps call sites clean)
        public void SendMoveBy(int entityId, int dx, int dy)
        {
            SendClientCommand(ClientCommand.MoveBy(entityId, dx, dy));
        }

        public void SendFlowFire(byte trigger, int param0)
        {
            SendClientCommand(ClientCommand.FlowFire(trigger, param0));
        }

        // -------------------------
        // Inbound
        // -------------------------

        public void Poll()
        {
            _transport.Poll();
        }

        private void OnReceive(object msg)
        {
            // Legacy harness path
            if (msg is ServerSnapshot snap)
            {
                Engine.ApplySnapshot(snap);
                return;
            }

            // Baseline/Ops path (if transport starts delivering these)
            if (msg is BaselineMsg baseline)
            {
                Engine.ApplyBaseline(baseline);
                return;
            }

            if (msg is ServerOpsMsg ops)
            {
                Engine.ApplyServerOps(ops);
                return;
            }
        }
    }
}
