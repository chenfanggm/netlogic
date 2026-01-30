using System.Collections.Generic;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.timing;

namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Engine boundary used by outer-ring loops (input/output/host).
    /// Note: only a single thread should call TickOnce (the engine thread).
    /// </summary>
    public interface IServerEngine
    {
        com.aqua.netlogic.sim.game.ServerModel ReadOnlyWorld { get; }

        int CurrentTick { get; }

        double ServerTimeMs { get; }

        void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<EngineCommand<EngineCommandType>> commands);

        void EnqueueServerCommands(
            List<EngineCommand<EngineCommandType>> commands, int requestedTick = -1);

        TickResult TickOnce(ServerTickContext ctx, bool includeSnapshot = false);

        ServerModelSnapshot BuildSnapshot();

        uint ComputeStateHash();
    }
}
