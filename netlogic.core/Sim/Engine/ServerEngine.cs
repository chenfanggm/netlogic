using System;
using System.Collections.Generic;
using Game;
using Sim.Commanding;
using Sim.Systems;

namespace Sim
{
    /// <summary>
    /// Pure authoritative simulation core.
    /// Responsibilities:
    /// - Own World mutation
    /// - Own authoritative tick counter
    /// - Execute systems in stable order
    /// - Produce snapshots
    ///
    /// Non-responsibilities:
    /// - Threading, sleeping, realtime scheduling (TickRunner/Host)
    /// - Input generation policies (InputPump)
    /// - Output formatting/printing (SnapshotFormatter/OutputPump)
    /// </summary>
    public sealed class ServerEngine : IServerEngine
    {
        public World ReadOnlyWorld => _world;

        public int CurrentServerTick => _currentTick;

        public long ServerTimeMs => _lastServerTimeMs;

        private int _currentTick;
        private long _lastServerTimeMs;

        private readonly World _world;
        private readonly CommandSystem<EngineCommandType> _commandSystem;
        private readonly ICommandSink<EngineCommandType>[] _systems;

        public ServerEngine(World initialWorld)
        {
            _world = initialWorld ?? throw new ArgumentNullException(nameof(initialWorld));

            // Stable system execution order matters for determinism.
            // GameFlowSystem first: flow transitions happen before gameplay systems.
            GameFlowSystem flow = new GameFlowSystem();
            MovementSystem movement = new MovementSystem();
            _systems = [
                flow,
                movement
            ];
            _commandSystem = new CommandSystem<EngineCommandType>(
                _systems,
                maxFutureTicks: 2,
                maxPastTicks: 2,
                maxStoredTicks: 16);

            _currentTick = 0;
            _lastServerTimeMs = 0;
        }

        public void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<EngineCommand<EngineCommandType>> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            int nowTick = CurrentServerTick;

            _commandSystem.Enqueue(
                connId: connId,
                clientRequestedTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: nowTick);
        }

        /// <summary>
        /// Enqueue server-generated commands for a given tick.
        /// connId is always 0 for server-generated commands.
        /// </summary>
        public void EnqueueServerCommands(
            List<EngineCommand<EngineCommandType>> commands, int requestedTick = -1)
        {
            if (commands == null || commands.Count == 0)
                return;

            int nowTick = CurrentServerTick;
            if (requestedTick == -1)
                requestedTick = nowTick + 1;

            _commandSystem.Enqueue(
                connId: 0,
                clientRequestedTick: requestedTick,
                clientCmdSeq: 0,
                commands: commands,
                currentServerTick: nowTick);
        }

        /// <summary>
        /// Execute exactly one authoritative tick.
        /// TickContext is provided by the runner (rate/time), but authoritative tick is owned by engine.
        /// </summary>
        public EngineTickResult TickOnce(TickContext ctx)
        {
            int tick = ++_currentTick;
            _lastServerTimeMs = ctx.ServerTimeMs;

            // 1) Execute systems in stable order
            _commandSystem.Execute(tick, _world);

            // 2) World fixed step
            _world.Advance(1);

            return new EngineTickResult(
                serverTick: tick,
                serverTimeMs: ctx.ServerTimeMs,
                snapshot: _world.BuildSnapshot(),
                reliableOps: []);
        }
    }
}
