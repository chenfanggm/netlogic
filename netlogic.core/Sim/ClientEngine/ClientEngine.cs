using System;
using System.Collections.Generic;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.sim.game.flow;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine
{
    /// <summary>
    /// ClientEngine = pure client-side state reconstruction core.
    /// Owns ClientModel.
    ///
    /// Consumes only client-facing replication primitives:
    /// - GameSnapshot (baseline)
    /// - ReplicationUpdate (RepOp[] + tick/hash/seq)
    ///
    /// No transport, no reliability, no wire decoding.
    /// </summary>
    public sealed class ClientEngine
    {
        private readonly IEventBus _eventBus;

        public ClientModel Model { get; } = new ClientModel();

        public int PlayerConnId { get; set; } = -1;

        private uint _clientCmdSeq = 1;

        /// <summary>
        /// Client's lead (in ticks) when estimating requested tick for commands.
        /// Last known server tick + InputLeadTicks = requestedClientTick hint.
        /// </summary>
        private int _inputLeadTicks = 1;

        /// <summary>
        /// Set true after the first baseline/snapshot has been applied.
        /// </summary>
        private bool _hasBaseline;

        // -----------------------------
        // Pending tick buffer (bounded)
        // -----------------------------
        private readonly SortedDictionary<int, TickResult> _pendingTicks = new SortedDictionary<int, TickResult>();
        private const int MaxPendingTicks = 120;
        private int _lastAppliedTick = -1;

        /// <summary>
        /// Client's best guess of when a command should take effect (server tick domain).
        /// Uses only last known server tick + lead; no server clock access.
        /// </summary>
        public int EstimateRequestedTick()
        {
            int baseTick = Model.LastServerTick;
            if (baseTick < 0)
                baseTick = 0;
            return baseTick + _inputLeadTicks;
        }

        public uint NextClientCmdSeq() => _clientCmdSeq++;

        public ClientEngine(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            Model.LastServerTick = -1;
        }

        internal void ApplyBaselineSnapshot(GameSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Model.ResetFromSnapshot(snapshot);
            _hasBaseline = true;
        }

        /// <summary>
        /// Contract entrypoint: apply a baseline snapshot payload.
        /// </summary>
        public void Apply(in BaselineResult baseline)
        {
            ApplyBaselineSnapshot(baseline.Snapshot);
        }

        /// <summary>
        /// Contract entrypoint: apply a TickResult that may include a baseline snapshot.
        /// </summary>
        public void Apply(in TickResult result)
        {
            if (_hasBaseline && _lastAppliedTick >= 0 && result.Tick <= _lastAppliedTick)
                return;

            if (result.Snapshot != null)
            {
                ApplyBaselineSnapshot(result.Snapshot);

                _lastAppliedTick = result.Tick;
                Model.LastServerTick = result.Tick;
                Model.LastStateHash = result.StateHash;

                ClearPendingUpTo(result.Tick);
                FlushPendingTicks();
                return;
            }

            if (!_hasBaseline)
            {
                BufferTick(result);
                return;
            }

            if (_lastAppliedTick < 0)
                _lastAppliedTick = Model.LastServerTick;

            if (result.Tick == _lastAppliedTick + 1)
            {
                ApplyReplicationResult(result);
                _lastAppliedTick = result.Tick;
                FlushPendingTicks();
                return;
            }

            BufferTick(result);
            FlushPendingTicks();
        }

        private void BufferTick(in TickResult result)
        {
            TickResult owned = result.WithOwnedOps();
            _pendingTicks[owned.Tick] = owned;

            while (_pendingTicks.Count > MaxPendingTicks)
            {
                int oldestTick = FirstKey(_pendingTicks);
                if (oldestTick == int.MinValue)
                    break;

                TickResult oldest = _pendingTicks[oldestTick];
                oldest.Dispose();
                _pendingTicks.Remove(oldestTick);
            }
        }

        private void FlushPendingTicks()
        {
            if (!_hasBaseline || _lastAppliedTick < 0)
                return;

            while (true)
            {
                int nextTick = _lastAppliedTick + 1;
                if (!_pendingTicks.TryGetValue(nextTick, out TickResult next))
                    break;

                _pendingTicks.Remove(nextTick);
                ApplyReplicationResult(next);
                _lastAppliedTick = nextTick;
                next.Dispose();
            }
        }

        private void ClearPendingUpTo(int tickInclusive)
        {
            if (_pendingTicks.Count == 0)
                return;

            List<int>? toRemove = null;
            foreach (KeyValuePair<int, TickResult> kv in _pendingTicks)
            {
                if (kv.Key <= tickInclusive)
                {
                    toRemove ??= new List<int>();
                    toRemove.Add(kv.Key);
                }
                else
                    break;
            }

            if (toRemove == null)
                return;

            for (int i = 0; i < toRemove.Count; i++)
            {
                int k = toRemove[i];
                TickResult r = _pendingTicks[k];
                r.Dispose();
                _pendingTicks.Remove(k);
            }
        }

        private static int FirstKey(SortedDictionary<int, TickResult> dict)
        {
            foreach (int k in dict.Keys)
                return k;
            return int.MinValue;
        }

        /// <summary>
        /// Contract entrypoint: apply one authoritative replication frame (ServerEngine output) directly.
        ///
        /// This bypasses wire encode/decode and is ideal for:
        /// - in-process harnesses
        /// - deterministic tests
        /// - replay tools (already decoded)
        ///
        /// Baselines remain explicit via Apply(BaselineResult).
        /// </summary>
        private void ApplyReplicationResult(in TickResult result)
        {
            ReadOnlySpan<RepOp> ops = result.Ops.Span;

            if (ops.Length == 0)
            {
                Model.LastServerTick = result.Tick;
                Model.LastStateHash = result.StateHash;
                return;
            }

            ApplyReplicationUpdateFiltered(
                serverTick: result.Tick,
                stateHash: result.StateHash,
                isReliable: true,
                ops: ops);

            ApplyReplicationUpdateFiltered(
                serverTick: result.Tick,
                stateHash: result.StateHash,
                isReliable: false,
                ops: ops);

            Model.LastServerTick = result.Tick;
            Model.LastStateHash = result.StateHash;
        }

        private void ApplyReplicationUpdateFiltered(
            int serverTick,
            uint stateHash,
            bool isReliable,
            ReadOnlySpan<RepOp> ops)
        {
            bool any = false;

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];

                if (isReliable)
                {
                    if (!IsReliableType(op.Type))
                        continue;
                }
                else
                {
                    if (op.Type != RepOpType.PositionSnapshot)
                        continue;
                }

                any = true;

                switch (op.Type)
                {
                    case RepOpType.PositionSnapshot:
                        Model.ApplyPositionSnapshot(op.A, op.B, op.C);
                        break;

                    case RepOpType.EntitySpawned:
                        Model.ApplyEntitySpawned(op.A, op.B, op.C, op.D);
                        break;

                    case RepOpType.EntityDestroyed:
                        Model.ApplyEntityDestroyed(op.A);
                        break;

                    case RepOpType.FlowSnapshot:
                        {
                            GameFlowState previousFlowState = (GameFlowState)Model.Flow.FlowState;
                            byte flowState = (byte)(op.A & 0xFF);
                            byte roundState = (byte)((op.A >> 8) & 0xFF);
                            byte lastCookMetTarget = (byte)((op.A >> 16) & 0xFF);
                            byte cookAttemptsUsed = (byte)((op.A >> 24) & 0xFF);

                            FlowSnapshot flow = new FlowSnapshot(
                                (GameFlowState)flowState,
                                op.B, // levelIndex
                                op.C, // roundIndex
                                op.D, // selectedChefHatId
                                op.E, // targetScore
                                op.F, // cumulativeScore
                                cookAttemptsUsed,
                                (com.aqua.netlogic.sim.game.flow.RoundState)roundState,
                                op.G, // cookResultSeq
                                op.H, // lastCookScoreDelta
                                lastCookMetTarget != 0);

                            Model.Flow.ApplyFlowSnapshot(flow);
                            if (flow.FlowState != previousFlowState)
                            {
                                _eventBus.Publish(new GameFlowStateTransitionEvent(
                                    previousFlowState,
                                    flow.FlowState,
                                    serverTick));
                            }
                            break;
                        }

                    case RepOpType.FlowFire:
                        break;

                    default:
                        break;
                }
            }

            if (!any)
                return;

            Model.LastServerTick = serverTick;
            Model.LastStateHash = stateHash;
        }

        internal void ApplyReplicationUpdate(ReplicationUpdate update)
        {
            RepOp[] ops = update.Ops;
            if (ops != null && ops.Length > 0)
            {
                for (int i = 0; i < ops.Length; i++)
                {
                    RepOp op = ops[i];

                    switch (op.Type)
                    {
                        case RepOpType.PositionSnapshot:
                            Model.ApplyPositionSnapshot(op.A, op.B, op.C);
                            break;

                        case RepOpType.EntitySpawned:
                            Model.ApplyEntitySpawned(op.A, op.B, op.C, op.D);
                            break;

                        case RepOpType.EntityDestroyed:
                            Model.ApplyEntityDestroyed(op.A);
                            break;

                        case RepOpType.FlowSnapshot:
                            {
                                GameFlowState previousFlowState = (GameFlowState)Model.Flow.FlowState;
                                byte flowState = (byte)(op.A & 0xFF);
                                byte roundState = (byte)((op.A >> 8) & 0xFF);
                                byte lastCookMetTarget = (byte)((op.A >> 16) & 0xFF);
                                byte cookAttemptsUsed = (byte)((op.A >> 24) & 0xFF);

                                FlowSnapshot flow = new FlowSnapshot(
                                    (GameFlowState)flowState,
                                    op.B, // levelIndex
                                    op.C, // roundIndex
                                    op.D, // selectedChefHatId
                                    op.E, // targetScore
                                    op.F, // cumulativeScore
                                    cookAttemptsUsed,
                                    (com.aqua.netlogic.sim.game.flow.RoundState)roundState,
                                    op.G, // cookResultSeq
                                    op.H, // lastCookScoreDelta
                                    lastCookMetTarget != 0);

                                Model.Flow.ApplyFlowSnapshot(flow);
                                if (flow.FlowState != previousFlowState)
                                {
                                    _eventBus.Publish(new GameFlowStateTransitionEvent(
                                        previousFlowState,
                                        flow.FlowState,
                                        update.ServerTick));
                                }
                                break;
                            }

                        case RepOpType.FlowFire:
                            // Optional: hook for client-side UI/FX.
                            break;

                        default:
                            break;
                    }
                }
            }

            Model.LastServerTick = update.ServerTick;
            Model.LastStateHash = update.StateHash;
        }

        private static bool IsReliableType(RepOpType t)
        {
            return t == RepOpType.EntitySpawned
                || t == RepOpType.EntityDestroyed
                || t == RepOpType.FlowFire
                || t == RepOpType.FlowSnapshot;
        }
    }
}
