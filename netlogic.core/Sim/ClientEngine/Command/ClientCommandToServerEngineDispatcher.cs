using System;
using System.Collections.Generic;
using com.aqua.netlogic.command;
using com.aqua.netlogic.command.ingress;

namespace com.aqua.netlogic.sim.clientengine.command
{
    /// <summary>
    /// Commander that enqueues commands directly into a ServerEngine.
    /// </summary>
    public sealed class ClientCommandToServerEngineDispatcher : IClientCommandDispatcher
    {
        private readonly IServerEngine _engine;

        public ClientCommandToServerEngineDispatcher(IServerEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            EngineCommand<EngineCommandType> command)
        {
            _engine.EnqueueCommands(
                connId,
                requestedClientTick,
                clientCmdSeq,
                new List<EngineCommand<EngineCommandType>>(1) { command });
        }
    }
}
