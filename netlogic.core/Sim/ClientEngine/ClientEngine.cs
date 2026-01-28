using System;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine
{
    /// <summary>
    /// ClientEngine = pure client-side state reconstruction core.
    /// Owns ClientModel, consumes GameSnapshot + ReplicationUpdate (RepOp[]).
    ///
    /// No transport, no wire decoding, no reliability.
    /// </summary>
    public sealed class ClientEngine
    {
        public ClientModel Model { get; } = new ClientModel();

        public void ApplyBaselineSnapshot(GameSnapshot snapshot, int serverTick, uint stateHash)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Model.ResetFromSnapshot(snapshot, serverTick, stateHash);
        }

        public void ApplyReplicationUpdate(ReplicationUpdate update)
        {
            // Apply ops (if any)
            RepOp[] ops = update.Ops;
            if (ops != null && ops.Length > 0)
            {
                for (int i = 0; i < ops.Length; i++)
                {
                    RepOp op = ops[i];

                    switch (op.Type)
                    {
                        case RepOpType.PositionSnapshot:
                            // A=id, B=x, C=y
                            Model.ApplyPositionSnapshot(op.A, op.B, op.C);
                            break;

                        case RepOpType.EntitySpawned:
                            // A=id, B=x, C=y, D=hp
                            Model.ApplyEntitySpawned(op.A, op.B, op.C, op.D);
                            break;

                        case RepOpType.EntityDestroyed:
                            // A=id
                            Model.ApplyEntityDestroyed(op.A);
                            break;

                        case RepOpType.FlowSnapshot:
                        {
                            // RepOp.FlowSnapshot packs bytes into A.
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

                        // Optional: if you ever want client-side reactions to FlowFire.
                        case RepOpType.FlowFire:
                            // Currently no model mutation required; keep it as a hook for UI/FX if desired.
                            break;

                        default:
                            break;
                    }
                }
            }

            // Always advance tick/hash (heartbeat included)
            Model.LastServerTick = update.ServerTick;
            Model.LastStateHash = update.StateHash;
        }

    }
}
