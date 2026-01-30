using System;
using System.Collections.Generic;
using System.Threading;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using com.aqua.netlogic.program.flowscript;
using com.aqua.netlogic.command;
using com.aqua.netlogic.eventbus;
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
            // ---------------------
            // Service Container
            // ---------------------
            ServiceCollection services = new ServiceCollection();
            services.AddMessagePipe();
            services.AddSingleton<IEventBus, MessagePipeEventBus>();
            services.AddTransient<ClientEngine>();


            IServiceProvider serviceProvider = services.BuildServiceProvider();
            GlobalMessagePipe.SetProvider(serviceProvider);

            // ---------------------
            // Event Bus
            // ---------------------
            IEventBus eventBus = serviceProvider.GetRequiredService<IEventBus>();

            // ---------------------
            // Client Engine
            // ---------------------
            ClientEngine clientEngine = serviceProvider.GetRequiredService<ClientEngine>();

            // ---------------------
            // Create authoritative world
            // ---------------------
            Game world = new Game();
            Entity playerEntity = world.CreateEntityAt(x: 0, y: 0);
            int playerEntityId = playerEntity.Id;

            // ---------------------
            // Server Engine
            // ---------------------
            ServerEngine serverEngine = new ServerEngine(world);

            // ---------------------
            // Bootstrap baseline (direct snapshot, no encode/decode)
            // ---------------------
            const int playerConnId = 1;
            RenderSimulator renderSim = new RenderSimulator
            {
                ClientCmdSeq = 0,
                LastClientFlowState = (GameFlowState)255,
                ExitingInRound = false,
                ExitMenuAtMs = -1,
                ExitAfterVictoryAtMs = -1,
                LastPrintAtMs = 0,
            };
            using IDisposable flowTransitionSub = eventBus.Subscribe(new FlowStateTransitionHandler(renderSim));

            // ---------------------
            // Bootstrap
            // ---------------------
            TickContext bootstrapCtx = new TickContext(serverTimeMs: 0, elapsedMsSinceLastTick: 0);
            using TickResult bootstrapResult = serverEngine.TickOnce(bootstrapCtx, includeSnapshot: true);
            clientEngine.Apply(bootstrapResult);

            // ---------------------
            // Drive ticks
            // ---------------------
            PlayerFlowScript flowScript = new PlayerFlowScript();
            using CancellationTokenSource cts = new CancellationTokenSource();
            if (config.MaxRunDuration.HasValue)
                cts.CancelAfter(config.MaxRunDuration.Value);
            TickRunner runner = new TickRunner(config.TickRateHz);
            runner.Run(
                   onTick: (TickContext ctx) => OnTick(ctx, serverEngine, clientEngine, playerConnId, playerEntityId, flowScript, renderSim, cts),
                   cts.Token);
        }

        private static void OnTick(
            TickContext ctx,
            ServerEngine serverEngine,
            ClientEngine clientEngine,
            int playerConnId,
            int playerEntityId,
            PlayerFlowScript flowScript,
            RenderSimulator renderSim,
            CancellationTokenSource cts)
        {
            // Tick engine
            using TickResult result = serverEngine.TickOnce(ctx);

            // Apply ServerEngine output directly to ClientEngine.
            renderSim.ResetFlowFlags();
            clientEngine.Apply(result);

            // Drive flow script using client-reconstructed model
            GameFlowState clientFlow = (GameFlowState)clientEngine.Model.Flow.FlowState;
            bool leftInRound = renderSim.LeftInRoundThisTick;
            bool enteredMainMenuAfterVictory = renderSim.EnteredMainMenuAfterVictoryThisTick;

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
                        clientCmdSeq: renderSim.ClientCmdSeq++,
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
                        clientCmdSeq: renderSim.ClientCmdSeq++,
                        commands: cmds);
                });

            if (clientFlow == GameFlowState.InRound && ctx.ServerTimeMs - renderSim.LastPrintAtMs >= 500)
            {
                renderSim.LastPrintAtMs = ctx.ServerTimeMs;
                if (clientEngine.Model.Entities.TryGetValue(playerEntityId, out EntityState e))
                    Console.WriteLine($"[ClientModel] InRound Entity {playerEntityId} pos=({e.X},{e.Y})");
            }

            // Log flow state transitions and periodic heartbeat (like the old harness).
            if (renderSim.FlowStateChangedThisTick || ctx.ServerTimeMs - renderSim.LastPrintAtMs >= 500)
            {
                renderSim.LastPrintAtMs = ctx.ServerTimeMs;

                Console.WriteLine(
                    $"[ClientModel] t={ctx.ServerTimeMs:0} serverTick={clientEngine.Model.LastServerTick} Flow={clientFlow}");
            }

            if (leftInRound && clientFlow != GameFlowState.MainMenu && clientFlow != GameFlowState.RunVictory)
            {
                renderSim.ExitingInRound = true;
                List<EngineCommand<EngineCommandType>> cmds =
                [
                    new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                ];

                serverEngine.EnqueueCommands(
                    connId: playerConnId,
                    requestedClientTick: serverEngine.CurrentTick + 1,
                    clientCmdSeq: renderSim.ClientCmdSeq++,
                    commands: cmds);
            }

            if (renderSim.ExitingInRound && clientFlow == GameFlowState.MainMenu)
            {
                if (renderSim.ExitMenuAtMs < 0)
                    renderSim.ExitMenuAtMs = ctx.ServerTimeMs + 1000;
                else if (ctx.ServerTimeMs >= renderSim.ExitMenuAtMs)
                    cts.Cancel();
            }

            if (enteredMainMenuAfterVictory)
                renderSim.ExitAfterVictoryAtMs = ctx.ServerTimeMs + 1000;

            if (renderSim.ExitAfterVictoryAtMs > 0 && ctx.ServerTimeMs >= renderSim.ExitAfterVictoryAtMs)
            {
                List<EngineCommand<EngineCommandType>> cmds =
                [
                    new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)
                ];

                serverEngine.EnqueueCommands(
                    connId: playerConnId,
                    requestedClientTick: serverEngine.CurrentTick + 1,
                    clientCmdSeq: renderSim.ClientCmdSeq++,
                    commands: cmds);
            }

            // End the harness once flow reaches Exit.
            if (clientFlow == GameFlowState.Exit)
                cts.Cancel();
        }

        private sealed class FlowStateTransitionHandler : IMessageHandler<GameFlowStateTransition>
        {
            private readonly RenderSimulator _state;

            public FlowStateTransitionHandler(RenderSimulator state)
            {
                _state = state;
            }

            public void Handle(GameFlowStateTransition message)
            {
                _state.FlowStateChangedThisTick = true;
                _state.LeftInRoundThisTick |= message.From == GameFlowState.InRound
                    && message.To != GameFlowState.InRound;
                _state.EnteredMainMenuAfterVictoryThisTick |= message.From == GameFlowState.RunVictory
                    && message.To == GameFlowState.MainMenu;
                _state.LastClientFlowState = message.To;
            }
        }
    }
}
