using System.Collections.Generic;

namespace Sim
{
    /// <summary>
    /// Engine boundary used by outer-ring loops (input/output/host).
    /// Note: only a single thread should call TickOnce (the engine thread).
    /// </summary>
    public interface IGameEngine
    {
        int CurrentTick { get; }

        double ServerTimeMs { get; }

        void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<EngineCommand<Game.EngineCommandType>> commands);

        void EnqueueServerCommands(
            List<EngineCommand<Game.EngineCommandType>> commands, int requestedTick = -1);

        EngineTickResult TickOnce(TickContext ctx);
    }
}
