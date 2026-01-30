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
    /// - ServerEngine output (TickResult + GameSnapshot)
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
            using TickResult bootstrapResult = serverEngine.TickOnce(bootstrapCtx, includeSnapshot: true);
            clientEngine.Apply(bootstrapResult);

            // ---------------------
            // Drive ticks
            // ---------------------
            TickHarnessState harnessState = new TickHarnessState
            {
                ClientCmdSeq = 0,
                LastClientFlowState = (GameFlowState)255,
                ExitingInRound = false,
                ExitMenuAtMs = -1,
                ExitAfterVictoryAtMs = -1,
                LastPrintAtMs = 0,
            };

            PlayerFlowScript flowScript = new PlayerFlowScript();
            using CancellationTokenSource cts = new CancellationTokenSource();
            if (config.MaxRunDuration.HasValue)
                cts.CancelAfter(config.MaxRunDuration.Value);
            TickRunner runner = new TickRunner(config.TickRateHz);
            runner.Run(
                onTick: (TickContext ctx) => OnTick(ctx, serverEngine, clientEngine, playerConnId, playerEntityId, flowScript, harnessState, cts),
                cts.Token);
        }

        private static void OnTick(
            TickContext ctx,
            ServerEngine serverEngine,
            ClientEngine clientEngine,
            int playerConnId,
            int playerEntityId,
            PlayerFlowScript flowScript,
            TickHarnessState state,
            CancellationTokenSource cts)
        {
            // Tick engine
            using TickResult result = serverEngine.TickOnce(ctx);

            // Apply ServerEngine output directly.
            clientEngine.Apply(result);

            // Drive flow script using client-reconstructed model
            GameFlowState clientFlow = (GameFlowState)clientEngine.Model.Flow.FlowState;
            bool leftInRound = (state.LastClientFlowState == GameFlowState.InRound) && (clientFlow != GameFlowState.InRound);
            bool enteredMainMenuAfterVictory = (state.LastClientFlowState == GameFlowState.RunVictory) && (clientFlow == GameFlowState.MainMenu);

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
                        clientCmdSeq: state.ClientCmdSeq++,
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
                        clientCmdSeq: state.ClientCmdSeq++,
                        commands: cmds);
                });

            if (clientFlow == GameFlowState.InRound && ctx.ServerTimeMs - state.LastPrintAtMs >= 500)
            {
                state.LastPrintAtMs = ctx.ServerTimeMs;
                if (clientEngine.Model.Entities.TryGetValue(playerEntityId, out EntityState e))
                    Console.WriteLine($"[ClientModel] InRound Entity {playerEntityId} pos=({e.X},{e.Y})");
            }

            // Log flow state transitions and periodic heartbeat (like the old harness).
            if (clientFlow != state.LastClientFlowState || ctx.ServerTimeMs - state.LastPrintAtMs >= 500)
            {
                state.LastPrintAtMs = ctx.ServerTimeMs;
                state.LastClientFlowState = clientFlow;

                Console.WriteLine(
                    $"[ClientModel] t={ctx.ServerTimeMs:0} serverTick={clientEngine.Model.LastServerTick} Flow={clientFlow}");
            }

            if (leftInRound && clientFlow != GameFlowState.MainMenu && clientFlow != GameFlowState.RunVictory)
            {
                state.ExitingInRound = true;
                List<EngineCommand<EngineCommandType>> cmds =
                [
                    new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                ];

                serverEngine.EnqueueCommands(
                    connId: playerConnId,
                    requestedClientTick: serverEngine.CurrentTick + 1,
                    clientCmdSeq: state.ClientCmdSeq++,
                    commands: cmds);
            }

            if (state.ExitingInRound && clientFlow == GameFlowState.MainMenu)
            {
                if (state.ExitMenuAtMs < 0)
                    state.ExitMenuAtMs = ctx.ServerTimeMs + 1000;
                else if (ctx.ServerTimeMs >= state.ExitMenuAtMs)
                    cts.Cancel();
            }

            if (enteredMainMenuAfterVictory)
                state.ExitAfterVictoryAtMs = ctx.ServerTimeMs + 1000;

            if (state.ExitAfterVictoryAtMs > 0 && ctx.ServerTimeMs >= state.ExitAfterVictoryAtMs)
            {
                List<EngineCommand<EngineCommandType>> cmds =
                [
                    new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                ];

                serverEngine.EnqueueCommands(
                    connId: playerConnId,
                    requestedClientTick: serverEngine.CurrentTick + 1,
                    clientCmdSeq: state.ClientCmdSeq++,
                    commands: cmds);
            }

            // End the harness once flow reaches Exit.
            if (clientFlow == GameFlowState.Exit)
                cts.Cancel();
        }

        private sealed class TickHarnessState
        {
            public uint ClientCmdSeq;
            public GameFlowState LastClientFlowState;
            public bool ExitingInRound;
            public double ExitMenuAtMs;
            public double ExitAfterVictoryAtMs;
            public double LastPrintAtMs;
        }
    }
}
