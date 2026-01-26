using Game;
using Sim;

namespace Program
{
    public static class AutoEngineTickProgram
    {
        public static void Run(TimeSpan? maxRunningDuration = null)
        {
            const int tickRateHz = 20;
            const int entityId = 1;
            const int connId = 1;

            // 1) Build authoritative world + engine (no transport)
            World world = new World();
            world.CreateEntityAt(entityId: entityId, x: 0, y: 0);

            IGameEngine engine = new GameEngine(world);

            // 2) Build outer-ring components (IO around the engine)
            TickRunner runner = new TickRunner(tickRateHz);
            IInputPump input = new MoveRightInputPump(connId, entityId, period: TimeSpan.FromMilliseconds(250));
            IOutputPump output = new ConsoleSnapshotOutput(
                entityId,
                period: TimeSpan.FromMilliseconds(500),
                formatter: new SnapshotFormatter(new EntityPositionReader()));
            LatestValue<EngineTickResult> latest = new LatestValue<EngineTickResult>();

            // 3) Host owns threads + lifecycle
            EngineHost host = new EngineHost(engine, runner, input, output, latest);

            using CancellationTokenSource cts = new CancellationTokenSource();
            if (maxRunningDuration.HasValue)
                cts.CancelAfter(maxRunningDuration.Value);
            host.Run(cts.Token);
        }
    }
}
