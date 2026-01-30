using MessagePipe;
using com.aqua.netlogic.sim.clientengine;

namespace com.aqua.netlogic.command.ingress
{
    public sealed class CommandEventHandler : IMessageHandler<CommandEvent>
    {
        private readonly ICommander _commander;
        private readonly ClientEngine _clientEngine;

        public CommandEventHandler(ICommander commander, ClientEngine clientEngine)
        {
            _commander = commander;
            _clientEngine = clientEngine;
        }

        public void Handle(CommandEvent message)
        {
            if (message == null)
                return;

            _commander.EnqueueCommands(
                connId: _clientEngine.PlayerConnId,
                requestedClientTick: _clientEngine.EstimateRequestedTick(),
                clientCmdSeq: _clientEngine.NextClientCmdSeq(),
                command: message.Command);
        }
    }
}
