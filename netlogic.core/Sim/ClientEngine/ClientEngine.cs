using System;
using System.Collections.Generic;
using com.aqua.netlogic.eventbus;
using com.aqua.netlogic.sim.clientengine.ops;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine
{
    /// <summary>
    /// ClientEngine = pure client-side state reconstruction core.
    /// Uses ClientModel (injected).
    ///
    /// Consumes only typed replication primitives:
    /// - BaselineResult (ServerModelSnapshot)
    /// - TickResult (server tick output)
    /// - ReplicationUpdate (decoded net update)
    ///
    /// No transport, no reliability, no wire decoding.
    /// </summary>
    public sealed class ClientEngine
    {
        private readonly IEventBus _eventBus;

        public ClientModel Model { get; }

        public int PlayerConnId { get; set; } = -1;

        private uint _clientCmdSeq = 1;
        private int _inputLeadTicks = 1;

        private bool _hasBaseline;
        private readonly ClientRepOpHandlers _opHandlers;

        // -----------------------------
        // Pending tick buffer (bounded)
        // -----------------------------
        private readonly SortedDictionary<int, BufferedTick> _pending = new SortedDictionary<int, BufferedTick>();
        private const int MaxPendingTicks = 120;
        private int _lastAppliedTick = -1;

        private readonly struct BufferedTick
        {
            public readonly int Tick;
            public readonly double ServerTimeMs;
            public readonly uint StateHash;
            public readonly RepOp[] Ops;

            public BufferedTick(int tick, double serverTimeMs, uint stateHash, RepOp[] ops)
            {
                Tick = tick;
                ServerTimeMs = serverTimeMs;
                StateHash = stateHash;
                Ops = ops;
            }
        }

        public int EstimateRequestedTick()
        {
            int baseTick = Model.LastServerTick;
            if (baseTick < 0) baseTick = 0;
            return baseTick + _inputLeadTicks;
        }

        public uint NextClientCmdSeq() => _clientCmdSeq++;

        public ClientEngine(IEventBus eventBus, ClientModel model)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Model.LastServerTick = -1;
            _opHandlers = ClientRepOpHandlerBootstrap.CreateDefault();
        }

        // -----------------------------
        // Baseline
        // -----------------------------

        internal void ApplyBaselineSnapshot(ServerModelSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            // Baseline must explicitly set FlowState inside ResetFromSnapshot.
            Model.ResetFromSnapshot(snapshot);

            _hasBaseline = true;

            int tick = snapshot.ServerTick;
            uint hash = snapshot.StateHash;

            _lastAppliedTick = tick;
            Model.LastServerTick = tick;
            Model.LastStateHash = hash;

            // Presentation bootstrap event (no fake "previous flow state").
            _eventBus.Publish(new BaselineAppliedEvent(tick, hash));

            // Any buffered ticks at/before baseline are invalid now.
            ClearPendingUpTo(tick);
            FlushPendingTicks();
        }

        public void Apply(in BaselineResult baseline)
        {
            ApplyBaselineSnapshot(baseline.Snapshot);
        }

        // -----------------------------
        // TickResult path (in-process or already-decoded)
        // -----------------------------

        public void Apply(in TickResult result)
        {
            Model.NowMs = result.ServerTimeMs;

            if (_hasBaseline && _lastAppliedTick >= 0 && result.Tick <= _lastAppliedTick)
                return;

            if (result.Snapshot != null)
            {
                ApplyBaselineSnapshot(result.Snapshot);
                return;
            }

            ApplyOrdered(
                tick: result.Tick,
                serverTimeMs: result.ServerTimeMs,
                stateHash: result.StateHash,
                ops: result.Ops.Span);
        }

        // -----------------------------
        // ReplicationUpdate path (decoded net)
        // -----------------------------

        internal void ApplyReplicationUpdate(ReplicationUpdate update)
        {
            ApplyOrdered(
                tick: update.ServerTick,
                serverTimeMs: Model.NowMs,
                stateHash: update.StateHash,
                ops: update.Ops);
        }

        // -----------------------------
        // Unified ordering + buffering
        // -----------------------------

        private void ApplyOrdered(int tick, double serverTimeMs, uint stateHash, ReadOnlySpan<RepOp> ops)
        {
            if (!_hasBaseline)
            {
                BufferOwned(tick, serverTimeMs, stateHash, ops);
                return;
            }

            if (_lastAppliedTick < 0)
                _lastAppliedTick = Model.LastServerTick;

            if (tick == _lastAppliedTick + 1)
            {
                ApplyOps(tick, serverTimeMs, stateHash, ops);
                _lastAppliedTick = tick;
                FlushPendingTicks();
                return;
            }

            BufferOwned(tick, serverTimeMs, stateHash, ops);
            FlushPendingTicks();
        }

        private void BufferOwned(int tick, double serverTimeMs, uint stateHash, ReadOnlySpan<RepOp> ops)
        {
            RepOp[] ownedOps = ops.Length == 0 ? Array.Empty<RepOp>() : ops.ToArray();
            _pending[tick] = new BufferedTick(tick, serverTimeMs, stateHash, ownedOps);

            while (_pending.Count > MaxPendingTicks)
            {
                int oldest = FirstKey(_pending);
                if (oldest == int.MinValue) break;
                _pending.Remove(oldest);
            }
        }

        private void FlushPendingTicks()
        {
            if (!_hasBaseline || _lastAppliedTick < 0)
                return;

            while (true)
            {
                int nextTick = _lastAppliedTick + 1;
                if (!_pending.TryGetValue(nextTick, out BufferedTick next))
                    break;

                _pending.Remove(nextTick);
                ApplyOps(next.Tick, next.ServerTimeMs, next.StateHash, next.Ops);
                _lastAppliedTick = nextTick;
            }
        }

        private void ClearPendingUpTo(int tickInclusive)
        {
            if (_pending.Count == 0)
                return;

            List<int>? toRemove = null;
            foreach (KeyValuePair<int, BufferedTick> kv in _pending)
            {
                if (kv.Key <= tickInclusive)
                {
                    toRemove ??= new List<int>();
                    toRemove.Add(kv.Key);
                }
                else break;
            }

            if (toRemove == null) return;

            for (int i = 0; i < toRemove.Count; i++)
                _pending.Remove(toRemove[i]);
        }

        private static int FirstKey(SortedDictionary<int, BufferedTick> dict)
        {
            foreach (int k in dict.Keys)
                return k;
            return int.MinValue;
        }

        // -----------------------------
        // Apply ops + derive presentation events
        // -----------------------------

        private void ApplyOps(int serverTick, double serverTimeMs, uint stateHash, ReadOnlySpan<RepOp> ops)
        {
            Model.NowMs = serverTimeMs;
            ClientOpContext ctx = new ClientOpContext(_eventBus, Model, serverTick);
            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];
                _opHandlers.Get(op.Type).Apply(ctx, op);
            }

            Model.LastServerTick = serverTick;
            Model.LastStateHash = stateHash;
        }
    }
}
