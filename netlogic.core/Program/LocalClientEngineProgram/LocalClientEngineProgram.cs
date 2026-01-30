using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using com.aqua.netlogic.program.flowscript;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.command.events;
using com.aqua.netlogic.sim.clientengine;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.entity;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.timing;
using com.aqua.netlogic.sim.clientengine.command;

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
            // Service Container
            // ---------------------
            ServiceCollection services = new ServiceCollection();
            services.AddMessagePipe();
            services.AddSingleton<IEventBus, MessagePipeEventBus>();
            services.AddSingleton<IServerEngine>(serverEngine);
            services.AddSingleton<IClientCommandDispatcher, ClientCommandToServerEngineDispatcher>();
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
            // Drive ticks
            // ---------------------
            PlayerFlowScript flowScript = new PlayerFlowScript(eventBus, clientEngine, playerEntityId);
            using CancellationTokenSource cts = new CancellationTokenSource();
            if (config.MaxRunDuration.HasValue)
                cts.CancelAfter(config.MaxRunDuration.Value);
            ServerTickRunner runner = new ServerTickRunner(config.TickRateHz);
            runner.Run(onTick: (ServerTickContext ctx) =>
            {
                // Tick engine
                using TickResult result = serverEngine.TickOnce(ctx);

                // Apply ServerEngine output directly to ClientEngine.
                clientEngine.Apply(result);

                // Drive flow script using client-reconstructed model
                GameFlowState clientFlowState = (GameFlowState)clientEngine.Model.Flow.FlowState;

                flowScript.Step(clientFlowState, ctx.NowMs);

                // End the harness once flow reaches Exit or max run duration is reached
                if (clientFlowState == GameFlowState.Exit
                || (config.MaxRunDuration.HasValue && TimeSpan.FromMilliseconds(ctx.NowMs) >= config.MaxRunDuration.Value))
                    cts.Cancel();
            }, cts.Token);
        }
    }
}
