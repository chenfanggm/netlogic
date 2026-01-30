using System;
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
        public ClientModel Model { get; } = new ClientModel();

        internal void ApplyBaselineSnapshot(GameSnapshot snapshot, int serverTick)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Model.ResetFromSnapshot(snapshot, serverTick);
        }

        /// <summary>
        /// Contract entrypoint: apply a TickFrame that may include a baseline snapshot.
        /// </summary>
        public void ApplyFrame(in TickFrame frame)
        {
            if (frame.Snapshot != null)
                ApplyBaselineSnapshot(frame.Snapshot, frame.Tick);

            ApplyFrame<TickFrame>(frame);
        }

        /// <summary>
        /// Contract entrypoint: apply one authoritative replication frame (ServerEngine output) directly.
        ///
        /// This bypasses wire encode/decode and is ideal for:
        /// - in-process harnesses
        /// - deterministic tests
        /// - replay tools (already decoded)
        ///
        /// Baselines remain explicit via ApplyBaselineSnapshot.
        /// </summary>
        public void ApplyFrame<TFrame>(in TFrame frame) where TFrame : struct, IReplicationFrame
        {
            ReadOnlySpan<RepOp> ops = frame.Ops.Span;

            if (ops.Length == 0)
            {
                Model.LastServerTick = frame.Tick;
                Model.LastStateHash = frame.StateHash;
                return;
            }

            // Partition by delivery semantics (parity with the network path).
            RepOp[] reliable = FilterReliable(ops);
            if (reliable.Length > 0)
            {
                ApplyReplicationUpdate(new ReplicationUpdate(
                    serverTick: frame.Tick,
                    serverSeq: 0,
                    stateHash: frame.StateHash,
                    isReliable: true,
                    ops: reliable));
            }

            RepOp[] unreliable = FilterUnreliable(ops);
            if (unreliable.Length > 0)
            {
                ApplyReplicationUpdate(new ReplicationUpdate(
                    serverTick: frame.Tick,
                    serverSeq: 0,
                    stateHash: frame.StateHash,
                    isReliable: false,
                    ops: unreliable));
            }

            // If no partition matched, still advance metadata.
            if (reliable.Length == 0 && unreliable.Length == 0)
            {
                Model.LastServerTick = frame.Tick;
                Model.LastStateHash = frame.StateHash;
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
                                byte flowState = (byte)(op.A & 0xFF);
                                byte roundState = (byte)((op.A >> 8) & 0xFF);
                                byte lastCookMetTarget = (byte)((op.A >> 16) & 0xFF);
                                byte cookAttemptsUsed = (byte)((op.A >> 24) & 0xFF);

                                FlowSnapshot flow = new FlowSnapshot(
                                    (com.aqua.netlogic.sim.game.flow.GameFlowState)flowState,
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
