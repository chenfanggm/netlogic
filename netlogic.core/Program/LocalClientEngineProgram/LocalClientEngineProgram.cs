using com.aqua.netlogic.sim.clientengine;
using com.aqua.netlogic.sim.clientengine.protocol;
using com.aqua.netlogic.net;
using com.aqua.netlogic.program.flowscript;
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.entity;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.systems.gameflowsystem.commands;
using com.aqua.netlogic.sim.systems.movementsystem.commands;
using com.aqua.netlogic.sim.timing;
using com.aqua.netlogic.sim.networkserver.protocol;

namespace com.aqua.netlogic.program
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
            // Create in-process encoder/decoder + client (NO transport)
            // ---------------------
            ServerMessageEncoder encoder = new ServerMessageEncoder();
            ClientMessageDecoder decoder = new ClientMessageDecoder();
            ClientEngine client = new ClientEngine();

            // Run one tick to produce initial state
            using TickFrame bootstrapFrame = engine.TickOnce(new TickContext(serverTimeMs: 0, elapsedMsSinceLastTick: 0));

            // Build baseline once and apply to client
            GameSnapshot bootstrapSnap = engine.BuildSnapshot();
            BaselineMsg baseline = ServerMessageEncoder.BuildBaseline(
                bootstrapSnap,
                bootstrapFrame.Tick,
                bootstrapFrame.WorldHash);

            GameSnapshot snap0 = decoder.DecodeBaselineToSnapshot(baseline, out int t0, out uint h0);
            client.ApplyBaselineSnapshot(snap0, t0, h0);

            // ---------------------
            // Drive ticks
            // ---------------------
            TickRunner runner = new TickRunner(config.TickRateHz);

            PlayerFlowScript flowScript = new PlayerFlowScript();
            uint clientCmdSeq = 1;
            uint reliableSeq = 0;
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

                        GameSnapshot resyncSnap = engine.BuildSnapshot();
                        uint resyncHash = engine.ComputeStateHash();
                        BaselineMsg resync = ServerMessageEncoder.BuildBaseline(
                            resyncSnap,
                            engine.CurrentTick,
                            resyncHash);

                        GameSnapshot snap = decoder.DecodeBaselineToSnapshot(resync, out int t, out uint h);
                        client.ApplyBaselineSnapshot(snap, t, h);
                    }

                    // Tick engine
                    using TickFrame frame = engine.TickOnce(ctx);

                    // Build reliable ops from RepOps (entity lifecycle + flow) and apply
                    {
                        encoder.EncodeReliableRepOpsToWriter(frame.Ops.Span, out ushort opCount);
                        if (opCount > 0)
                        {
                            byte[] payload = encoder.Writer.CopyData();
                            ServerOpsMsg reliableMsg = new ServerOpsMsg(
                                ProtocolVersion.Current,
                                HashContract.ScopeId,
                                (byte)HashContract.Phase,
                                frame.Tick,
                                ++reliableSeq,
                                frame.WorldHash,
                                opCount,
                                payload);

                            ReplicationUpdate up = decoder.DecodeServerOpsToUpdate(reliableMsg, isReliableLane: true);
                            client.ApplyReplicationUpdate(up);
                        }
                    }

                    // Build unreliable ops from RepOps (position snapshots) and apply
                    {
                        encoder.EncodeUnreliablePositionSnapshotsToWriter(frame.Ops.Span, out ushort opCount);
                        byte[] payload = (opCount == 0) ? Array.Empty<byte>() : encoder.Writer.CopyData();
                        ServerOpsMsg unreliableMsg = new ServerOpsMsg(
                            ProtocolVersion.Current,
                            HashContract.ScopeId,
                            (byte)HashContract.Phase,
                            frame.Tick,
                            0,
                            frame.WorldHash,
                            opCount,
                            payload);

                        ReplicationUpdate up = decoder.DecodeServerOpsToUpdate(unreliableMsg, isReliableLane: false);
                        client.ApplyReplicationUpdate(up);
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
