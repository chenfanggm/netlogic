using System;
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

        /// <summary>
        /// Client's lead (in ticks) when estimating requested tick for commands.
        /// Last known server tick + InputLeadTicks = requestedClientTick hint.
        /// </summary>
        private int _inputLeadTicks = 1;

        /// <summary>
        /// Client's best guess of when a command should take effect (server tick domain).
        /// Uses only last known server tick + lead; no server clock access.
        /// </summary>
        public int EstimateRequestedTick() => Model.LastServerTick + _inputLeadTicks;

        public ClientEngine(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        internal void ApplyBaselineSnapshot(GameSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Model.ResetFromSnapshot(snapshot);
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
            if (result.Snapshot != null)
                ApplyBaselineSnapshot(result.Snapshot);

            ApplyReplicationResult(result);
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

            // Partition by delivery semantics (parity with the network path).
            RepOp[] reliable = FilterReliable(ops);
            if (reliable.Length > 0)
            {
                ApplyReplicationUpdate(new ReplicationUpdate(
                    serverTick: result.Tick,
                    serverSeq: 0,
                    stateHash: result.StateHash,
                    isReliable: true,
                    ops: reliable));
            }

            RepOp[] unreliable = FilterUnreliable(ops);
            if (unreliable.Length > 0)
            {
                ApplyReplicationUpdate(new ReplicationUpdate(
                    serverTick: result.Tick,
                    serverSeq: 0,
                    stateHash: result.StateHash,
                    isReliable: false,
                    ops: unreliable));
            }

            // If no partition matched, still advance metadata.
            if (reliable.Length == 0 && unreliable.Length == 0)
            {
                Model.LastServerTick = result.Tick;
                Model.LastStateHash = result.StateHash;
            }
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
                                    _eventBus.Publish(new GameFlowStateTransition(
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

        private static RepOp[] FilterReliable(ReadOnlySpan<RepOp> ops)
        {
            int count = 0;
            for (int i = 0; i < ops.Length; i++)
            {
                if (IsReliableType(ops[i].Type))
                    count++;
            }

            if (count == 0)
                return Array.Empty<RepOp>();

            RepOp[] dst = new RepOp[count];
            int w = 0;
            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];
                if (IsReliableType(op.Type))
                    dst[w++] = op;
            }
            return dst;
        }

        private static RepOp[] FilterUnreliable(ReadOnlySpan<RepOp> ops)
        {
            int count = 0;
            for (int i = 0; i < ops.Length; i++)
            {
                if (ops[i].Type == RepOpType.PositionSnapshot)
                    count++;
            }

            if (count == 0)
                return Array.Empty<RepOp>();

            RepOp[] dst = new RepOp[count];
            int w = 0;
            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];
                if (op.Type == RepOpType.PositionSnapshot)
                    dst[w++] = op;
            }
            return dst;
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
