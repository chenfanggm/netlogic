using System;
using com.aqua.netlogic.sim.clientengine;
using com.aqua.netlogic.sim.replication;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.sim.clientengine.inprocess
{
    /// <summary>
    /// In-process bridge: lets ClientEngine consume ServerEngine outputs directly,
    /// without any wire encoding/decoding.
    ///
    /// Lane split is preserved for parity with the real network path:
    /// - Reliable: entity lifecycle + flow ops
    /// - Unreliable: position snapshots (latest-wins)
    /// </summary>
    public static class InProcessReplicationAdapter
    {
        public static void ApplyFrameToClient(in TickFrame frame, ClientEngine client, ref uint reliableSeq)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            ReadOnlySpan<RepOp> ops = frame.Ops.Span;
            if (ops.Length == 0)
                return;

            // Reliable lane
            RepOp[] reliable = FilterReliable(ops);
            if (reliable.Length > 0)
            {
                ReplicationUpdate up = new ReplicationUpdate(
                    serverTick: frame.Tick,
                    serverSeq: ++reliableSeq,
                    stateHash: frame.StateHash,
                    isReliable: true,
                    ops: reliable);

                client.ApplyReplicationUpdate(up);
            }

            // Unreliable lane
            RepOp[] unreliable = FilterUnreliable(ops);
            if (unreliable.Length > 0)
            {
                ReplicationUpdate up = new ReplicationUpdate(
                    serverTick: frame.Tick,
                    serverSeq: 0,
                    stateHash: frame.StateHash,
                    isReliable: false,
                    ops: unreliable);

                client.ApplyReplicationUpdate(up);
            }
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
