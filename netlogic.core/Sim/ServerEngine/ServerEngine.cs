using System;
using System.Collections.Generic;
using com.aqua.netlogic.command;
using com.aqua.netlogic.command.sink;
using com.aqua.netlogic.sim.game;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.systems.gameflowsystem;
using com.aqua.netlogic.sim.systems.movementsystem;
using com.aqua.netlogic.sim.timing;

namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Pure authoritative simulation core (server-side).
    /// Responsibilities:
    /// - Own World mutation
    /// - Own authoritative tick counter
    /// - Execute systems in stable order
    /// - Produce replication ops (RepOpBatch) + state hash
    ///
    /// Non-responsibilities:
    /// - Threading, sleeping, realtime scheduling (TickRunner/Host)
    /// - Input generation policies (InputPump)
    /// - Output formatting/printing (SnapshotFormatter/OutputPump)
    /// - Network transport, ack/replay, session management (NetworkServer)
    /// </summary>
    public class ServerEngine : IServerEngine
    {
        public com.aqua.netlogic.sim.game.Game ReadOnlyWorld => _game;

        public int CurrentTick => _currentTick;

        public double ServerTimeMs => _lastServerTimeMs;

        private int _currentTick;
        private double _lastServerTimeMs;

        private readonly com.aqua.netlogic.sim.game.Game _game;
        private readonly CommandSystem<EngineCommandType> _commandSystem;
        private readonly ICommandSink<EngineCommandType>[] _commandSinks;

        private readonly IReplicationRecorder _replication;

        // Flow replication (emit only on change)
        private FlowSnapshot _lastFlowSnap;
        private bool _hasLastFlowSnap;

        public ServerEngine(com.aqua.netlogic.sim.game.Game initialGame)
        {
            _game = initialGame ?? throw new ArgumentNullException(nameof(initialGame));

            // Stable system execution order matters for determinism.
            // GameFlowSystem first: flow transitions happen before gameplay systems.
            GameFlowSystem flow = new GameFlowSystem();
            MovementSystem movement = new MovementSystem();

            _commandSinks =
            [
                flow,
                movement
            ];

            _commandSystem = new CommandSystem<EngineCommandType>(
                _commandSinks,
                maxFutureTicks: 2,
                maxPastTicks: 2,
                maxStoredTicks: 16);

            _replication = new ReplicationRecorder(initialCapacity: 256);

            _currentTick = 0;
            _lastServerTimeMs = 0;

            _lastFlowSnap = default;
            _hasLastFlowSnap = false;
        }

        /// <summary>
        /// Execute exactly one authoritative tick.
        /// </summary>
        public TickResult TickOnce(ServerTickContext ctx, bool includeSnapshot = false)
        {
            int tick = ++_currentTick;
            _lastServerTimeMs = ctx.ServerTimeMs;

            _replication.BeginTick(tick);

            // Provide transient replication hook to systems for this tick.
            _game.SetReplicator(new WorldReplicator(_replication));

            // 1) Execute systems in stable order
            _commandSystem.Execute(tick, _game);

            // 2) Game fixed step (lifecycle + deterministic per-tick logic)
            _game.Advance(1);

            // 3) Flow replication: reliable flow snapshot when changed
            FlowSnapshot flowSnapshot = _game.BuildFlowSnapshot();
            EmitFlowIfChanged(flowSnapshot);

            // 4) Finalize
            RepOpBatch ops = _replication.EndTickAndFlush();

            // Clear transient hook
            _game.SetReplicator(null);

            // 5) World hash AFTER applying tick
            uint worldHash = com.aqua.netlogic.sim.game.WorldHash.Compute(_game);

            bool shouldIncludeSnapshot = includeSnapshot || tick == 1;
            GameSnapshot? snapshot = shouldIncludeSnapshot ? _game.Snapshot(tick, worldHash) : null;

            return new TickResult(
                tick: tick,
                serverTimeMs: _lastServerTimeMs,
                stateHash: worldHash,
                ops: ops,
                snapshot: snapshot);
        }

        /// <summary>
        /// Builds an engine snapshot for baseline building / debug tools.
        /// This is intentionally NOT part of TickResult to avoid coupling server tick output
        /// to internal snapshot structures.
        /// </summary>
        public GameSnapshot BuildSnapshot()
        {
            return _game.Snapshot();
        }

        /// <summary>
        /// Computes authoritative hash at the current moment (post-tick state).
        /// Server can use this for baselines.
        /// </summary>
        public uint ComputeStateHash()
        {
            return com.aqua.netlogic.sim.game.WorldHash.Compute(_game);
        }

        private void EmitFlowIfChanged(in FlowSnapshot flow)
        {
            if (_hasLastFlowSnap && _lastFlowSnap == flow)
                return;

            _hasLastFlowSnap = true;
            _lastFlowSnap = flow;

            _replication.Record(RepOp.FlowSnapshot(
                flowState: (byte)flow.FlowState,
                roundState: (byte)flow.RoundState,
                lastCookMetTarget: (byte)(flow.LastCookMetTarget ? 1 : 0),
                cookAttemptsUsed: (byte)flow.CookAttemptsUsed,
                levelIndex: flow.LevelIndex,
                roundIndex: flow.RoundIndex,
                selectedChefHatId: flow.SelectedChefHatId,
                targetScore: flow.TargetScore,
                cumulativeScore: flow.CumulativeScore,
                cookResultSeq: flow.CookResultSeq,
                lastCookScoreDelta: flow.LastCookScoreDelta));
        }

        public void EnqueueCommands(
            int connId,
            int requestedClientTick,
            uint clientCmdSeq,
            List<EngineCommand<EngineCommandType>> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            _commandSystem.Enqueue(
                connId: connId,
                clientRequestedTick: requestedClientTick,
                clientCmdSeq: clientCmdSeq,
                commands: commands,
                currentServerTick: CurrentTick);
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

            if (requestedTick == -1)
                requestedTick = CurrentTick + 1;

            _commandSystem.Enqueue(
                connId: 0,
                clientRequestedTick: requestedTick,
                clientCmdSeq: 0,
                commands: commands,
                currentServerTick: CurrentTick);
        }

        public void GetCommandBufferStats(
            out long droppedTooOld,
            out long snappedLate,
            out long clampedFuture,
            out long accepted)
        {
            _commandSystem.GetCommandBufferStats(
                out droppedTooOld,
                out snappedLate,
                out clampedFuture,
                out accepted);
        }
    }
}
