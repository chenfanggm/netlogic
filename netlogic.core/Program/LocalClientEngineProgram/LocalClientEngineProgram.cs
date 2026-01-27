using Client2.Game;
using Client2.Net;
using Net;
using Sim.Command;
using Sim.Engine;
using Sim.Game;
using Sim.System;
using Sim.Time;

namespace Program
{
    /// <summary>
    /// In-process full-stack WITHOUT:
    /// - network transport
    /// - reliable stream replay/ack
    /// - rendering
    ///
    /// It tests:
    /// - GameEngine determinism loop
    /// - server output building (baseline + ops)
    /// - GameClient2 applying baseline + ops to rebuild ClientModel
    /// </summary>
    public static class LocalClientEngineProgram
    {
        public static void Run(TimeSpan? maxRunningDuration = null)
        {
            const int tickRateHz = 20;
            const int entityId = 1;
            const int connId = 1;

            // ---------------------
            // Create authoritative world + engine
            // ---------------------
            Game world = new Game();
            world.CreateEntityAt(entityId: entityId, x: 0, y: 0);

            GameEngine engine = new GameEngine(world);

            // ---------------------
            // Create in-process feed + client
            // ---------------------
            InProcessNetFeed feed = new InProcessNetFeed();
            GameClient2 client = new GameClient2(feed);

            InProcessServerEmitter emitter = new InProcessServerEmitter();

            // Force a snapshot for baseline seed
            engine.RequestSnapshot();

            // Run one tick to produce the snapshot
            TickContext bootstrapCtx = new TickContext(serverTimeMs: 0, elapsedMsSinceLastTick: 0);
            TickFrame bootstrapFrame = engine.TickOnce(bootstrapCtx);

            if (bootstrapFrame.Snapshot == null)
                throw new Exception("Expected snapshot on bootstrap tick, but got null.");

            // Build baseline from snapshot instead of world iteration (future-proof)
            BaselineMsg baseline = emitter.BuildBaselineFromSnapshot(
                serverTick: bootstrapFrame.Tick,
                stateHash: bootstrapFrame.StateHash,
                snapshot: bootstrapFrame.Snapshot!);
            feed.PushBaseline(baseline);

            // ---------------------
            // Drive ticks
            // ---------------------
            TickRunner runner = new TickRunner(tickRateHz);

            long lastMoveAtMs = 0;
            long lastPrintAtMs = 0;
            long lastResyncAtMs = 0;

            uint clientCmdSeq = 1;

            using CancellationTokenSource cts = new CancellationTokenSource();
            if (maxRunningDuration.HasValue)
                cts.CancelAfter(maxRunningDuration.Value);

            runner.Run(
                onTick: (TickContext ctx) =>
                {
                    // Inject a server command (as if client sent it) every 250ms.
                    if ((long)ctx.ServerTimeMs - lastMoveAtMs >= 250)
                    {
                        lastMoveAtMs = (long)ctx.ServerTimeMs;

                        List<EngineCommand<EngineCommandType>> cmds =
                        [
                            new MoveByEngineCommand(entityId: entityId, dx: 1, dy: 0)
                        ];

                        engine.EnqueueCommands(
                            connId: connId,
                            requestedClientTick: engine.CurrentTick + 1,
                            clientCmdSeq: clientCmdSeq++,
                            commands: cmds);
                    }

                    // Periodic resync every 5 seconds (for testing correctness)
                    if ((long)ctx.ServerTimeMs - lastResyncAtMs >= 5000)
                    {
                        lastResyncAtMs = (long)ctx.ServerTimeMs;
                        engine.RequestSnapshot();
                    }

                    // Tick engine
                    TickFrame frame = engine.TickOnce(ctx);

                    // If snapshot emitted this tick, treat it as resync baseline
                    if (frame.Snapshot != null)
                    {
                        BaselineMsg resyncBaseline = emitter.BuildBaselineFromSnapshot(
                            serverTick: frame.Tick,
                            stateHash: frame.StateHash,
                            snapshot: frame.Snapshot!);
                        feed.PushBaseline(resyncBaseline);
                    }

                    // Build sample ops from recorded replication ops and feed them to client
                    ServerOpsMsg sampleOps = emitter.BuildSampleOpsFromRepOps(frame.Tick, frame.StateHash, frame.Ops);
                    feed.PushOps(sampleOps, Lane.Sample);

                    // Build reliable flow ops from recorded replication ops and feed them to client
                    if (emitter.TryBuildReliableOpsFromRepOps(frame.Tick, frame.StateHash, frame.Ops, out ServerOpsMsg relOps))
                        feed.PushOps(relOps, Lane.Reliable);

                    // Print every 500ms
                    if ((long)ctx.ServerTimeMs - lastPrintAtMs >= 500)
                    {
                        lastPrintAtMs = (long)ctx.ServerTimeMs;

                        Console.WriteLine($"[ClientModel] tick={client.Model.LastServerTick} hash={client.Model.LastStateHash}");

                        foreach (EntityState e in client.Model.Entities.Values)
                            Console.WriteLine($"  Entity {e.Id} pos=({e.X},{e.Y}) hp={e.Hp}");

                        Console.WriteLine(
                            $"  FlowState={client.Model.Flow.FlowState} RoundState={client.Model.Flow.RoundState} " +
                            $"Level={client.Model.Flow.LevelIndex} Round={client.Model.Flow.RoundIndex} " +
                            $"Target={client.Model.Flow.TargetScore} Cum={client.Model.Flow.CumulativeScore} " +
                            $"CookSeq={client.Model.Flow.CookResultSeq} Delta={client.Model.Flow.LastCookScoreDelta} " +
                            $"Met={client.Model.Flow.LastCookMetTarget}");
                    }
                },
                token: cts.Token);
        }
    }
}
