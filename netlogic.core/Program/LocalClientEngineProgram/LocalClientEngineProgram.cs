using System.Collections.Generic;
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
            InProcessClientTransport transport = new InProcessClientTransport(connId: connId);
            NetworkClient2 net = new NetworkClient2(transport, tickRateHz);
            GameClient2 client = new GameClient2(net);
            client.Start();
            client.Connect(host: "local", port: 0);

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
            byte[] baselineBytes = MsgCodec.EncodeBaseline(baseline);
            transport.Enqueue(Lane.Reliable, baselineBytes);

            if (emitter.TryBuildReliableFlowSnapshot(bootstrapFrame.Tick, engine.ReadOnlyWorld, out ServerOpsMsg bootstrapReliable))
            {
                byte[] bootstrapRelBytes = MsgCodec.EncodeServerOps(Lane.Reliable, bootstrapReliable);
                transport.Enqueue(Lane.Reliable, bootstrapRelBytes);
            }

            client.Poll();

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
                        byte[] resyncBytes = MsgCodec.EncodeBaseline(resyncBaseline);
                        transport.Enqueue(Lane.Reliable, resyncBytes);

                        if (emitter.TryBuildReliableFlowSnapshot(frame.Tick, engine.ReadOnlyWorld, out ServerOpsMsg rel0))
                        {
                            byte[] rel0Bytes = MsgCodec.EncodeServerOps(Lane.Reliable, rel0);
                            transport.Enqueue(Lane.Reliable, rel0Bytes);
                        }
                    }

                    // Build sample ops from recorded replication ops and feed them to client
                    ServerOpsMsg sampleOps = emitter.BuildSampleOpsFromRepOps(frame.Tick, frame.StateHash, frame.Ops);
                    byte[] sampleBytes = MsgCodec.EncodeServerOps(Lane.Sample, sampleOps);
                    transport.Enqueue(Lane.Sample, sampleBytes);

                    // Build reliable flow snapshot (on change) and feed it to client
                    if (emitter.TryBuildReliableFlowSnapshot(frame.Tick, engine.ReadOnlyWorld, out ServerOpsMsg relOps))
                    {
                        byte[] relBytes = MsgCodec.EncodeServerOps(Lane.Reliable, relOps);
                        transport.Enqueue(Lane.Reliable, relBytes);
                    }

                    client.Poll();

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
    internal sealed class InProcessClientTransport : IClientTransport
    {
        private readonly Queue<NetPacket> _incoming = new Queue<NetPacket>();
        private readonly int _connId;
        private bool _connected;

        public InProcessClientTransport(int connId)
        {
            _connId = connId;
            _connected = true;
        }

        public bool IsConnected => _connected;

        public void Start() { }

        public void Connect(string host, int port)
        {
            _connected = true;
        }

        public void Poll() { }

        public bool TryReceive(out NetPacket packet)
        {
            if (_incoming.Count > 0)
            {
                packet = _incoming.Dequeue();
                return true;
            }

            packet = default;
            return false;
        }

        public void Send(Lane lane, ArraySegment<byte> payload)
        {
            // No-op for in-process client harness.
        }

        public void Enqueue(Lane lane, byte[] payload)
        {
            _incoming.Enqueue(new NetPacket(_connId, lane, new ArraySegment<byte>(payload, 0, payload.Length)));
        }

        public void Dispose()
        {
            _incoming.Clear();
            _connected = false;
        }
    }
}
