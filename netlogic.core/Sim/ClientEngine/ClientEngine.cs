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
    /// Uses ClientModel (injected).
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

        public ClientModel Model { get; }

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

        public ClientEngine(IEventBus eventBus, ClientModel model)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Model.LastServerTick = -1;
        }

        internal void ApplyBaselineSnapshot(ServerModelSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Model.ResetFromSnapshot(snapshot);
            if (Model.FlowState == default)
                Model.FlowState = GameFlowState.Boot;
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
            Model.NowMs = result.ServerTimeMs;

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
            Model.NowMs = result.ServerTimeMs;
            ReadOnlySpan<RepOp> ops = result.Ops.Span;

            ApplyOps(result.Tick, result.StateHash, ops);
        }

        internal void ApplyReplicationUpdate(ReplicationUpdate update)
        {
            RepOp[] ops = update.Ops;
            ApplyOps(update.ServerTick, update.StateHash, ops);
        }

        private void ApplyOps(int serverTick, uint stateHash, ReadOnlySpan<RepOp> ops)
        {
            GameFlowState prevFlow = Model.FlowState;

            if (ops.Length == 0)
            {
                Model.LastServerTick = serverTick;
                Model.LastStateHash = stateHash;
                return;
            }

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];
                RepOpApplier.ApplyAuthoritative(Model, op);
            }

            Model.LastServerTick = serverTick;
            Model.LastStateHash = stateHash;

            GameFlowState nextFlow = Model.FlowState;
            if (nextFlow != prevFlow)
            {
                _eventBus.Publish(new GameFlowStateTransitionEvent(
                    prevFlow,
                    nextFlow,
                    serverTick));
            }
        }
    }
}
