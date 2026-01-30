using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine.command
{
    /// <summary>
    /// Abstracts how client-authored commands are delivered to the server.
    /// </summary>
    public interface IClientCommandDispatcher
    {
        void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            EngineCommand<EngineCommandType> command);
    }
}
