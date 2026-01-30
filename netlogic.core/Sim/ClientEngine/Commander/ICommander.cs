using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.command.ingress
{
    /// <summary>
    /// Abstracts how client-authored commands are delivered to the server.
    /// </summary>
    public interface ICommander
    {
        void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            EngineCommand<EngineCommandType> command);
    }
}
