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
            ServerEngine engine = new ServerEngine(world);
            ClientEngine client = new ClientEngine();

            // ---------------------
            // Bootstrap baseline (direct snapshot, no encode/decode)
            // ---------------------
            TickContext bootstrapCtx = new TickContext(serverTimeMs: 0, elapsedMsSinceLastTick: 0);
            using TickFrame bootstrapFrame = engine.TickOnce(bootstrapCtx);

            GameSnapshot bootstrapSnap = engine.BuildSnapshot();
            client.ApplyBaselineSnapshot(bootstrapSnap, bootstrapFrame.Tick, bootstrapFrame.StateHash);

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
                    // Optional periodic resync every 5 seconds (tests correctness).
                    if ((long)ctx.ServerTimeMs - lastResyncAtMs >= 5000)
                    {
                        lastResyncAtMs = (long)ctx.ServerTimeMs;

                        GameSnapshot resyncSnap = engine.BuildSnapshot();
                        uint resyncHash = engine.ComputeStateHash();
                        client.ApplyBaselineSnapshot(resyncSnap, engine.CurrentTick, resyncHash);
                    }

                    // Tick engine
                    using TickFrame frame = engine.TickOnce(ctx);

                    // Apply ServerEngine output directly (TickFrame implements IReplicationFrame)
                    client.ApplyFrame(frame);

                    // Drive flow script using client-reconstructed model
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
                        lastPrintAtMs = (long)ctx.ServerTimeMs;
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
                    }

                    lastClientFlowState = clientFlow;
                },
                cts.Token);
        }
    }
}
