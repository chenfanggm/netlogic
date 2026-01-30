using System;
using com.aqua.netlogic.command;
using com.aqua.netlogic.command.ingress;
using com.aqua.netlogic.sim.clientengine.protocol;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.networkclient
{
    /// <summary>
    /// Commander that forwards engine commands over a NetworkClient.
    /// </summary>
    public sealed class NetworkClientCommander : ICommander
    {
        private readonly NetworkClient _client;

        public NetworkClientCommander(NetworkClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            EngineCommand<EngineCommandType> command)
        {
            if (command == null)
                return;

            ClientCommand[] clientCommands = EngineCommandToClientCommandConverter.ConvertToNewArray(command);
            if (clientCommands.Length == 0)
                return;

            _client.SendCommands(clientCommands);
        }
    }
}
