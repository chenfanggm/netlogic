using Sim;
using Game;

namespace Program
{
    public interface IInputPump
    {
        void Run(IGameEngine engine, CancellationToken token);
    }

    /// <summary>
    /// Simple deterministic input: enqueue MoveBy(+1,0) every period.
    /// Responsibility boundary:
    /// - Produces commands only (does not tick the engine, does not print).
    /// </summary>
    public sealed class MoveRightInputPump(int connId, int entityId, TimeSpan period) : IInputPump
    {
        private readonly int _connId = connId;
        private readonly int _entityId = entityId;
        private readonly TimeSpan _period = period;

        public void Run(IGameEngine engine, CancellationToken token)
        {
            uint seq = 0;

            while (!token.IsCancellationRequested)
            {
                // If your CommandSystem expects "requested tick", keep that policy here
                // (input policy), not in the engine tick loop.
                int requestedTick = engine.CurrentServerTick + 1;

                List<EngineCommand<EngineCommandType>> cmds =
                [
                    new MoveByEngineCommand(entityId: _entityId, dx: 1, dy: 0)
                ];

                engine.EnqueueCommands(
                    connId: _connId,
                    requestedClientTick: requestedTick,
                    clientCmdSeq: seq++,
                    commands: cmds);

                SleepRespectingCancel(_period, token);
            }
        }

        private static void SleepRespectingCancel(TimeSpan duration, CancellationToken token)
        {
            // Avoid Thread.Sleep swallowing cancellation responsiveness on long durations.
            const int sliceMs = 25;
            int remaining = (int)duration.TotalMilliseconds;

            while (remaining > 0 && !token.IsCancellationRequested)
            {
                int step = remaining > sliceMs ? sliceMs : remaining;
                Thread.Sleep(step);
                remaining -= step;
            }
        }
    }
}
