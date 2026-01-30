using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using com.aqua.netlogic.program.flowscript;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.command.ingress;
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
            // Authoritative world
            // ---------------------
            Game world = new Game();
            Entity playerEntity = world.CreateEntityAt(x: 0, y: 0);
            int playerEntityId = playerEntity.Id;

            // ---------------------
            // Server Engine
            // ---------------------
            ServerEngine serverEngine = new ServerEngine(world);

            // ---------------------
            // Render Simulator
            // ---------------------
            RenderSimulator renderSim = new RenderSimulator
            {
                LastClientFlowState = (GameFlowState)255,
                ExitingInRound = false,
                ExitMenuAtMs = -1,
                ExitAfterVictoryAtMs = -1,
                LastPrintAtMs = 0,
            };

            // ---------------------
            // Service Container
            // ---------------------
            ServiceCollection services = new ServiceCollection();
            services.AddMessagePipe();
            services.AddSingleton<IEventBus, MessagePipeEventBus>();
            services.AddSingleton<IServerEngine>(serverEngine);
            services.AddSingleton<IClientCommandDispatcher, ClientCommandToServerEngineDispatcher>();
            services.AddSingleton(renderSim);
            services.AddSingleton<ClientEngine>();
            services.AddTransient<CommandEventHandler>();
            services.AddTransient<FlowStateTransitionEventHandler>();

            IServiceProvider serviceProvider = services.BuildServiceProvider();
            GlobalMessagePipe.SetProvider(serviceProvider);

            // ---------------------
            // Event Bus
            // ---------------------
            IEventBus eventBus = serviceProvider.GetRequiredService<IEventBus>();
            DisposableBagBuilder disposableBag = DisposableBag.CreateBuilder();
            disposableBag.Add(eventBus.Subscribe(serviceProvider.GetRequiredService<FlowStateTransitionEventHandler>()));
            disposableBag.Add(eventBus.Subscribe(serviceProvider.GetRequiredService<CommandEventHandler>()));
            using IDisposable disposable = disposableBag.Build();

            // ---------------------
            // Client Engine
            // ---------------------
            ClientEngine clientEngine = serviceProvider.GetRequiredService<ClientEngine>();
            clientEngine.PlayerConnId = 1;

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
                   onTick: (TickContext ctx) => OnTick(ctx, serverEngine, clientEngine, eventBus, playerEntityId, flowScript, renderSim, cts),
                   cts.Token);
        }

        private static void OnTick(
            TickContext ctx,
            ServerEngine serverEngine,
            ClientEngine clientEngine,
            IEventBus eventBus,
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

            flowScript.Step(
                clientFlow,
                ctx.ServerTimeMs,
                fireIntent: (intent, param0) =>
                {
                    eventBus.Publish(new CommandEvent(
                        new FlowIntentEngineCommand(intent, param0)));
                },
                move: () =>
                {
                    eventBus.Publish(new CommandEvent(
                        new MoveByEngineCommand(entityId: playerEntityId, dx: 1, dy: 0)));
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

            if (renderSim.LeftInRoundThisTick && clientFlow != GameFlowState.MainMenu && clientFlow != GameFlowState.RunVictory)
            {
                renderSim.ExitingInRound = true;
                eventBus.Publish(new CommandEvent(
                    new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)));
            }

            if (renderSim.ExitingInRound && clientFlow == GameFlowState.MainMenu)
            {
                if (renderSim.ExitMenuAtMs < 0)
                    renderSim.ExitMenuAtMs = ctx.ServerTimeMs + 1000;
                else if (ctx.ServerTimeMs >= renderSim.ExitMenuAtMs)
                    cts.Cancel();
            }

            if (renderSim.EnteredMainMenuAfterVictoryThisTick)
                renderSim.ExitAfterVictoryAtMs = ctx.ServerTimeMs + 1000;

            if (renderSim.ExitAfterVictoryAtMs > 0 && ctx.ServerTimeMs >= renderSim.ExitAfterVictoryAtMs)
            {
                eventBus.Publish(new CommandEvent(
                    new FlowIntentEngineCommand(GameFlowIntent.ReturnToMenu, 0)));
            }

            // End the harness once flow reaches Exit.
            if (clientFlow == GameFlowState.Exit)
                cts.Cancel();
        }

    }
}
