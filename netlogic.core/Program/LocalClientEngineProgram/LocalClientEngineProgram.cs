using Client.Game;
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
    /// - ServerEngine determinism loop
    /// - server output building (baseline + ops)
    /// - ClientEngine applying baseline + ops to rebuild ClientModel
    /// </summary>
    public sealed class LocalClientEngineProgram : IProgram
    {
        public void Run(ProgramConfig config)
        {
            const int playerConnId = 1;

            // ---------------------
            // Create authoritative world
            // ---------------------
            Game world = new Game();
            Entity playerEntity = world.CreateEntityAt(x: 0, y: 0);
            int playerEntityId = playerEntity.Id;

            // ---------------------
            // Create engine
            // ---------------------
            ServerEngine engine = new ServerEngine(world);

            // ---------------------
            // Create in-process emitter + client (NO transport)
            // ---------------------
            InProcessServerEmitter server = new InProcessServerEmitter();
            ClientEngine client = new ClientEngine();

            // Run one tick to produce initial state
            TickContext bootstrapCtx = new TickContext(serverTimeMs: 0, elapsedMsSinceLastTick: 0);
            using TickFrame bootstrapFrame = engine.TickOnce(bootstrapCtx);

            // Build baseline once and apply to client
            BaselineMsg baseline = server.BuildBaseline(
                serverTick: bootstrapFrame.Tick,
                world: engine.ReadOnlyWorld);

            client.ApplyBaseline(baseline);

            // ---------------------
            // Drive ticks
            // ---------------------
            TickRunner runner = new TickRunner(config.TickRateHz);

            PlayerFlowScript flowScript = new PlayerFlowScript();
            uint clientCmdSeq = 1;
            GameFlowState lastClientFlowState = (GameFlowState)255;
            bool exitingInRound = false;
            long exitMenuAtMs = -1;
            long exitAfterVictoryAtMs = -1;
            long lastPrintAtMs = 0;
            long lastResyncAtMs = 0;

            using CancellationTokenSource cts = new CancellationTokenSource();
            if (config.MaxRunDuration.HasValue)
                cts.CancelAfter(config.MaxRunDuration.Value);

            runner.Run(
                onTick: (TickContext ctx) =>
                {
                    // Periodic resync every 5 seconds (for testing correctness)
                    if ((long)ctx.ServerTimeMs - lastResyncAtMs >= 5000)
                    {
                        lastResyncAtMs = (long)ctx.ServerTimeMs;

                        BaselineMsg resync = server.BuildBaseline(
                            serverTick: engine.CurrentTick,
                            world: engine.ReadOnlyWorld);

                        client.ApplyBaseline(resync);
                    }

                    // Tick engine
                    using TickFrame frame = engine.TickOnce(ctx);

                    // Build reliable ops from RepOps (entity lifecycle + flow) and apply
                    {
                        RepOp[] reliableScan = frame.Ops.ToArray(); // emitter expects array for reliable build
                        if (server.TryBuildReliableOpsFromRepOps(frame.Tick, frame.StateHash, reliableScan, out ServerOpsMsg reliableMsg))
                            client.ApplyServerOps(reliableMsg);
                    }

                    // Build unreliable ops from RepOps (position snapshots) and apply
                    {
                        ServerOpsMsg unreliableMsg = server.BuildUnreliableOpsFromRepOps(
                            serverTick: frame.Tick,
                            stateHash: frame.StateHash,
                            ops: frame.Ops.Span);

                        client.ApplyServerOps(unreliableMsg);
                    }

                    GameFlowState clientFlow = (GameFlowState)client.Model.Flow.FlowState;
                    bool leftInRound = (lastClientFlowState == GameFlowState.InRound) && (clientFlow != GameFlowState.InRound);
                    bool enteredMainMenuAfterVictory = (lastClientFlowState == GameFlowState.RunVictory) && (clientFlow == GameFlowState.MainMenu);

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
                                connId: playerConnId,
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
                                connId: playerConnId,
                                requestedClientTick: engine.CurrentTick + 1,
                                clientCmdSeq: clientCmdSeq++,
                                commands: cmds);
                        });

                    if (clientFlow == GameFlowState.InRound && ctx.ServerTimeMs - lastPrintAtMs >= 500)
                    {
                        if (client.Model.Entities.TryGetValue(playerEntityId, out EntityState e))
                            Console.WriteLine($"[ClientModel] InRound Entity {playerEntityId} pos=({e.X},{e.Y})");
                    }

                    if (leftInRound && clientFlow != GameFlowState.MainMenu && clientFlow != GameFlowState.RunVictory)
                    {
                        exitingInRound = true;
                        List<EngineCommand<EngineCommandType>> cmds =
                        [
                            new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                        ];

                        engine.EnqueueCommands(
                            connId: playerConnId,
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

                    if (enteredMainMenuAfterVictory)
                        exitAfterVictoryAtMs = (long)ctx.ServerTimeMs + 1000;

                    if (exitAfterVictoryAtMs > 0 && (long)ctx.ServerTimeMs >= exitAfterVictoryAtMs)
                    {
                        List<EngineCommand<EngineCommandType>> cmds =
                        [
                            new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                        ];

                        engine.EnqueueCommands(
                            connId: playerConnId,
                            requestedClientTick: engine.CurrentTick + 1,
                            clientCmdSeq: clientCmdSeq++,
                            commands: cmds);

                        exitAfterVictoryAtMs = -1;
                    }

                    if (clientFlow == GameFlowState.Exit)
                        cts.Cancel();

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
