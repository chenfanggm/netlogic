using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using com.aqua.netlogic.program.flowscript;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.command.events;
using com.aqua.netlogic.sim.clientengine;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.timing;
using com.aqua.netlogic.sim.clientengine.command;
using com.aqua.netlogic.sim.replication;

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
            ServerModel world = new ServerModel();
            int playerEntityId = world.AllocateEntityId();
            RepOpApplier.ApplyAuthoritative(
                world,
                RepOp.EntitySpawned(playerEntityId, x: 0, y: 0, hp: 100));

            // ---------------------
            // Replay world (OPS ONLY)
            // ---------------------
            ServerModel replayWorld = new ServerModel();
            RepOpApplier.ApplyAuthoritative(
                replayWorld,
                RepOp.EntitySpawned(playerEntityId, x: 0, y: 0, hp: 100));

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
            services.AddSingleton<ClientModel>();
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
            ClientModel clientModel = serviceProvider.GetRequiredService<ClientModel>();
            clientEngine.PlayerConnId = 1;
            BaselineResult baseline = new BaselineResult(serverEngine.BuildSnapshot());
            clientEngine.Apply(baseline);

            // ---------------------
            // Drive ticks
            // ---------------------
            PlayerFlowScript flowScript = new PlayerFlowScript(eventBus, clientModel, playerEntityId);
            using CancellationTokenSource cts = new CancellationTokenSource();
            if (config.MaxRunDuration.HasValue)
                cts.CancelAfter(config.MaxRunDuration.Value);
            ServerTickRunner runner = new ServerTickRunner(config.TickRateHz);
            runner.Run(onTick: (ServerTickContext ctx) =>
            {
                // Tick engine
                using TickResult result = serverEngine.TickOnce(ctx);
                // Update client model
                clientEngine.Apply(result);

                // ---------------------
                // OPS-ONLY REPLAY
                // ---------------------
                ReadOnlySpan<RepOp> ops = result.Ops.Span;
                for (int i = 0; i < ops.Length; i++)
                {
                    RepOpApplier.ApplyAuthoritative(replayWorld, ops[i]);
                }

                // ---------------------
                // DETERMINISM CHECK
                // ---------------------
                uint serverHash = result.StateHash;
                uint replayHash = ServerModelHash.Compute(replayWorld);

                if (serverHash != replayHash)
                {
                    throw new InvalidOperationException(
                        $"[DeterminismViolation] Tick={result.Tick} " +
                        $"ServerHash={serverHash} ReplayHash={replayHash}");
                }

#if DEBUG
                uint recomputed = ServerModelHash.Compute(world);
                if (recomputed != serverHash)
                {
                    throw new InvalidOperationException(
                        $"[ServerHashMismatch] Tick={result.Tick} " +
                        $"TickResultHash={serverHash} RecomputedHash={recomputed}");
                }
#endif

                // Simulate player movement
                flowScript.Step();

                // Exit check (End the harness once flow reaches Exit or max run duration is reached)
                GameFlowState clientFlowState = clientModel.FlowState;
                if (clientFlowState == GameFlowState.Exit || (config.MaxRunDuration.HasValue
                    && TimeSpan.FromMilliseconds(ctx.NowMs) >= config.MaxRunDuration.Value))
                    cts.Cancel();
            }, cts.Token);
        }
    }
}
