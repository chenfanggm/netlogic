using System;
using System.Collections.Generic;
using System.Threading;
using com.aqua.netlogic.program.flowscript;
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.clientengine;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.entity;
using com.aqua.netlogic.net;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.systems.gameflowsystem.commands;
using com.aqua.netlogic.sim.systems.movementsystem.commands;
using com.aqua.netlogic.sim.timing;

namespace com.aqua.netlogic.program
{
    /// <summary>
    /// In-process full-stack WITHOUT:
    /// - network transport
    /// - reliable stream replay/ack
    /// - wire encode/decode
    /// - rendering
    ///
    /// It tests:
    /// - ServerEngine determinism loop
    /// - ServerEngine output (TickFrame + GameSnapshot)
    /// - ClientEngine consuming ServerEngine outputs directly
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
            // Create engine + client
            // ---------------------
            ServerEngine serverEngine = new ServerEngine(world);
            ClientEngine clientEngine = new ClientEngine();

            // ---------------------
            // Bootstrap baseline (direct snapshot, no encode/decode)
            // ---------------------
            TickContext bootstrapCtx = new TickContext(serverTimeMs: 0, elapsedMsSinceLastTick: 0);
            using TickFrame bootstrapFrame = serverEngine.TickOnce(bootstrapCtx, includeSnapshot: true);
            clientEngine.ApplyFrame(bootstrapFrame);

            // ---------------------
            // Drive ticks
            // ---------------------
            TickRunner runner = new TickRunner(config.TickRateHz);

            PlayerFlowScript flowScript = new PlayerFlowScript();
            uint clientCmdSeq = 0;
            GameFlowState lastClientFlowState = (GameFlowState)255;
            bool exitingInRound = false;
            double exitMenuAtMs = -1;
            double exitAfterVictoryAtMs = -1;
            double lastPrintAtMs = 0;
            double lastResyncAtMs = 0;

            using CancellationTokenSource cts = new CancellationTokenSource();
            if (config.MaxRunDuration.HasValue)
                cts.CancelAfter(config.MaxRunDuration.Value);

            runner.Run(
                onTick: (TickContext ctx) =>
                {
                    // Optional periodic resync every 5 seconds (tests correctness).
                    if (ctx.ServerTimeMs - lastResyncAtMs >= 5000)
                    {
                        lastResyncAtMs = ctx.ServerTimeMs;

                        GameSnapshot resyncSnap = serverEngine.BuildSnapshot();
                        uint resyncHash = serverEngine.ComputeStateHash();
                        clientEngine.ApplyBaselineSnapshot(resyncSnap, serverEngine.CurrentTick, resyncHash);
                    }

                    // Tick engine
                    using TickFrame frame = serverEngine.TickOnce(ctx);

                    // Apply ServerEngine output directly (TickFrame implements IReplicationFrame)
                    clientEngine.ApplyFrame(frame);

                    // Drive flow script using client-reconstructed model
                    GameFlowState clientFlow = (GameFlowState)clientEngine.Model.Flow.FlowState;
                    bool leftInRound = (lastClientFlowState == GameFlowState.InRound) && (clientFlow != GameFlowState.InRound);
                    bool enteredMainMenuAfterVictory = (lastClientFlowState == GameFlowState.RunVictory) && (clientFlow == GameFlowState.MainMenu);

                    flowScript.Step(
                        clientFlow,
                        ctx.ServerTimeMs,
                        fireIntent: (intent, param0) =>
                        {
                            List<EngineCommand<EngineCommandType>> cmds =
                            [
                                new FlowIntentEngineCommand(intent, param0)
                            ];

                            serverEngine.EnqueueCommands(
                                connId: playerConnId,
                                requestedClientTick: serverEngine.CurrentTick + 1,
                                clientCmdSeq: clientCmdSeq++,
                                commands: cmds);
                        },
                        move: () =>
                        {
                            List<EngineCommand<EngineCommandType>> cmds =
                            [
                                new MoveByEngineCommand(entityId: playerEntityId, dx: 1, dy: 0)
                            ];

                            serverEngine.EnqueueCommands(
                                connId: playerConnId,
                                requestedClientTick: serverEngine.CurrentTick + 1,
                                clientCmdSeq: clientCmdSeq++,
                                commands: cmds);
                        });

                    if (clientFlow == GameFlowState.InRound && ctx.ServerTimeMs - lastPrintAtMs >= 500)
                    {
                        lastPrintAtMs = ctx.ServerTimeMs;
                        if (clientEngine.Model.Entities.TryGetValue(playerEntityId, out EntityState e))
                            Console.WriteLine($"[ClientModel] InRound Entity {playerEntityId} pos=({e.X},{e.Y})");
                    }

                    // Log flow state transitions and periodic heartbeat (like the old harness).
                    if (clientFlow != lastClientFlowState || ctx.ServerTimeMs - lastPrintAtMs >= 500)
                    {
                        lastPrintAtMs = ctx.ServerTimeMs;
                        lastClientFlowState = clientFlow;

                        Console.WriteLine(
                            $"[ClientModel] t={ctx.ServerTimeMs:0} serverTick={clientEngine.Model.LastServerTick} Flow={clientFlow}");
                    }

                    if (leftInRound && clientFlow != GameFlowState.MainMenu && clientFlow != GameFlowState.RunVictory)
                    {
                        exitingInRound = true;
                        List<EngineCommand<EngineCommandType>> cmds =
                        [
                            new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                        ];

                        serverEngine.EnqueueCommands(
                            connId: playerConnId,
                            requestedClientTick: serverEngine.CurrentTick + 1,
                            clientCmdSeq: clientCmdSeq++,
                            commands: cmds);
                    }

                    if (exitingInRound && clientFlow == GameFlowState.MainMenu)
                    {
                        if (exitMenuAtMs < 0)
                            exitMenuAtMs = ctx.ServerTimeMs + 1000;
                        else if (ctx.ServerTimeMs >= exitMenuAtMs)
                            cts.Cancel();
                    }

                    if (enteredMainMenuAfterVictory)
                        exitAfterVictoryAtMs = ctx.ServerTimeMs + 1000;

                    if (exitAfterVictoryAtMs > 0 && ctx.ServerTimeMs >= exitAfterVictoryAtMs)
                    {
                        List<EngineCommand<EngineCommandType>> cmds =
                        [
                            new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                        ];

                        serverEngine.EnqueueCommands(
                            connId: playerConnId,
                            requestedClientTick: serverEngine.CurrentTick + 1,
                            clientCmdSeq: clientCmdSeq++,
                            commands: cmds);
                    }

                    // End the harness once flow reaches Exit.
                    if (clientFlow == GameFlowState.Exit)
                        cts.Cancel();
                },
                cts.Token);
        }
    }
}
