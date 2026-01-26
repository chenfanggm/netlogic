using Sim.Game;
using Sim.Command;
using Sim.Time;

namespace Sim.Engine
{
    /// <summary>
    /// Engine boundary used by outer-ring loops (input/output/host).
    /// Note: only a single thread should call TickOnce (the engine thread).
    /// </summary>
    public interface IGameEngine
    {
        Game.Game ReadOnlyWorld { get; }

        int CurrentTick { get; }

        double ServerTimeMs { get; }

        void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<EngineCommand<EngineCommandType>> commands);

        void EnqueueServerCommands(
            List<EngineCommand<EngineCommandType>> commands, int requestedTick = -1);

        EngineTickResult TickOnce(TickContext ctx);
    }
}
