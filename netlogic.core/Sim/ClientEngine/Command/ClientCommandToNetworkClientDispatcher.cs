using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.clientengine.protocol;
using com.aqua.netlogic.sim.networkclient;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine.command
{
    /// <summary>
    /// Commander that forwards engine commands over a NetworkClient.
    /// </summary>
    public sealed class ClientCommandToNetworkClientDispatcher : IClientCommandDispatcher
    {
        private readonly NetworkClient _client;

        public ClientCommandToNetworkClientDispatcher(NetworkClient client)
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
