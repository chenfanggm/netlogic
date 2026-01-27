using Program.FlowScript;
using Sim.Command;
using Sim.Engine;
using Sim.Game;
using Sim.Game.Flow;
using Sim.System;
using Sim.Time;

namespace Program
{
    public static class LocalEngineProgram
    {
        public static void Run(TimeSpan? maxRunningDuration = null)
        {
            const int tickRateHz = 20;
            const int entityId = 1;
            const int connId = 1;
            const int playerEntityId = 1;

            // 1) Build authoritative world + engine (no transport)
            Game game = new Game();
            game.CreateEntityAt(entityId: entityId, x: 0, y: 0);

            GameEngine engine = new GameEngine(game);

            // 2) Build outer-ring components (IO around the engine)
            TickRunner runner = new TickRunner(tickRateHz);
            PlayerFlowScript flowScript = new PlayerFlowScript();
            uint clientCmdSeq = 1;
            GameFlowState lastFlowState = (GameFlowState)255;
            bool exitingInRound = false;
            long exitMenuAtMs = -1;
            long exitAfterVictoryAtMs = -1;
            long lastPrintAtMs = 0;

            using CancellationTokenSource cts = new CancellationTokenSource();
            if (maxRunningDuration.HasValue)
                cts.CancelAfter(maxRunningDuration.Value);

            runner.Run(
                onTick: (TickContext ctx) =>
                {
                    engine.TickOnce(ctx);

                    GameFlowState flowState = engine.ReadOnlyWorld.BuildFlowSnapshot().FlowState;
                    bool leftInRound = (lastFlowState == GameFlowState.InRound) && (flowState != GameFlowState.InRound);
                    bool enteredMainMenuAfterVictory = (lastFlowState == GameFlowState.RunVictory) && (flowState == GameFlowState.MainMenu);

                    flowScript.Step(
                        flowState,
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

                    if (leftInRound && flowState != GameFlowState.MainMenu && flowState != GameFlowState.RunVictory)
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

                    if (exitingInRound && flowState == GameFlowState.MainMenu)
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
                            connId: connId,
                            requestedClientTick: engine.CurrentTick + 1,
                            clientCmdSeq: clientCmdSeq++,
                            commands: cmds);

                        exitAfterVictoryAtMs = -1;
                    }

                    if (flowState == GameFlowState.Exit)
                        cts.Cancel();

                    if (flowState == GameFlowState.InRound)
                    {
                        foreach (Entity e in engine.ReadOnlyWorld.Entities)
                        {
                            if (e.Id == playerEntityId)
                            {
                                Console.WriteLine($"[Engine] InRound Entity {playerEntityId} pos=({e.X},{e.Y})");
                                break;
                            }
                        }
                    }

                    if (flowState != lastFlowState || ctx.ServerTimeMs - lastPrintAtMs >= 250)
                    {
                        lastFlowState = flowState;
                        lastPrintAtMs = (long)ctx.ServerTimeMs;

                        Console.WriteLine(
                            $"[Engine] t={ctx.ServerTimeMs:0} tick={engine.CurrentTick} Flow={flowState}");
                    }
                },
                token: cts.Token);
        }
    }
}
