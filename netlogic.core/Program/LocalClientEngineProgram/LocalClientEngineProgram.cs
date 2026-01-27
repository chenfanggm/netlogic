using Client2.Game;
using Client2.Net;
using Net;
using Program.FlowScript;
using Sim.Command;
using Sim.Engine;
using Sim.Game;
using Sim.Game.Flow;
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
            const int playerEntityId = 1;

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

            if (emitter.TryBuildReliableFlowSnapshot(bootstrapFrame.Tick, engine.ReadOnlyWorld, out ServerOpsMsg bootstrapReliable))
                feed.PushOps(bootstrapReliable, Lane.Reliable);

            // ---------------------
            // Drive ticks
            // ---------------------
            TickRunner runner = new TickRunner(tickRateHz);

            PlayerFlowScript flowScript = new PlayerFlowScript();
            uint clientCmdSeq = 1;
            GameFlowState lastClientFlowState = (GameFlowState)255;
            bool exitingInRound = false;
            long exitMenuAtMs = -1;
            long lastPrintAtMs = 0;
            long lastResyncAtMs = 0;

            using CancellationTokenSource cts = new CancellationTokenSource();
            if (maxRunningDuration.HasValue)
                cts.CancelAfter(maxRunningDuration.Value);

            runner.Run(
                onTick: (TickContext ctx) =>
                {
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

                        if (emitter.TryBuildReliableFlowSnapshot(frame.Tick, engine.ReadOnlyWorld, out ServerOpsMsg rel0))
                            feed.PushOps(rel0, Lane.Reliable);
                    }

                    // Build sample ops from recorded replication ops and feed them to client
                    ServerOpsMsg sampleOps = emitter.BuildSampleOpsFromRepOps(frame.Tick, frame.StateHash, frame.Ops);
                    feed.PushOps(sampleOps, Lane.Sample);

                    // Build reliable flow snapshot (on change) and feed it to client
                    if (emitter.TryBuildReliableFlowSnapshot(frame.Tick, engine.ReadOnlyWorld, out ServerOpsMsg relOps))
                        feed.PushOps(relOps, Lane.Reliable);

                    GameFlowState clientFlow = (GameFlowState)client.Model.Flow.FlowState;
                    bool leftInRound = (lastClientFlowState == GameFlowState.InRound) && (clientFlow != GameFlowState.InRound);

                    flowScript.Step(
                        clientFlow,
                        (long)ctx.ServerTimeMs,
                        fireIntent: (intent, param0) =>
                        {
                            List<EngineCommand<EngineCommandType>> cmds =
                            [
                                new FlowIntentEngineCommand(intent, param0)
                            ];

                            engine.EnqueueCommands(
                                connId: connId,
                                requestedClientTick: engine.CurrentTick + 1,
                                clientCmdSeq: clientCmdSeq++,
                                commands: cmds);
                        },
                        move: () =>
                        {
                            List<EngineCommand<EngineCommandType>> cmds =
                            [
                                new MoveByEngineCommand(entityId: playerEntityId, dx: 1, dy: 0)
                            ];

                            engine.EnqueueCommands(
                                connId: connId,
                                requestedClientTick: engine.CurrentTick + 1,
                                clientCmdSeq: clientCmdSeq++,
                                commands: cmds);
                        });

                    if (clientFlow == GameFlowState.InRound && ctx.ServerTimeMs - lastPrintAtMs >= 500)
                    {
                        if (client.Model.Entities.TryGetValue(playerEntityId, out EntityState e))
                            Console.WriteLine($"[ClientModel] InRound Entity {playerEntityId} pos=({e.X},{e.Y})");
                    }

                    if (leftInRound && clientFlow != GameFlowState.MainMenu)
                    {
                        exitingInRound = true;
                        List<EngineCommand<EngineCommandType>> cmds =
                        [
                            new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                        ];

                        engine.EnqueueCommands(
                            connId: connId,
                            requestedClientTick: engine.CurrentTick + 1,
                            clientCmdSeq: clientCmdSeq++,
                            commands: cmds);
                    }

                    if (exitingInRound && clientFlow == GameFlowState.MainMenu)
                    {
                        if (exitMenuAtMs < 0)
                            exitMenuAtMs = (long)ctx.ServerTimeMs + 1000;
                        else if ((long)ctx.ServerTimeMs >= exitMenuAtMs)
                            cts.Cancel();
                    }

                    if (clientFlow != lastClientFlowState || ctx.ServerTimeMs - lastPrintAtMs >= 500)
                    {
                        lastClientFlowState = clientFlow;
                        lastPrintAtMs = (long)ctx.ServerTimeMs;

                        Console.WriteLine(
                            $"[ClientModel] t={ctx.ServerTimeMs:0} serverTick={client.Model.LastServerTick} Flow={clientFlow}");
                    }
                },
                token: cts.Token);
        }
    }
}
