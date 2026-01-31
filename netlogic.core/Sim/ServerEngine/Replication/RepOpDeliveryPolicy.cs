using System;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.serverengine.replication
{
    internal static class RepOpDeliveryPolicy
    {
        internal enum Delivery : byte
        {
            Reliable = 0,
            /// <summary>
            /// Unreliable, latest-wins state deltas.
            /// </summary>
            Unreliable = 1,
        }

        public static Delivery DeliveryOf(RepOpType type) => type switch
        {
            // Reliable lane (entity lifecycle)
            RepOpType.EntitySpawned => Delivery.Reliable,
            RepOpType.EntityDestroyed => Delivery.Reliable,

            // Unreliable lane
            RepOpType.PositionSnapshot => Delivery.Unreliable,

            // Reliable lane (flow/UI control)
            RepOpType.FlowFire => Delivery.Reliable,
            RepOpType.FlowSnapshot => Delivery.Reliable,

            RepOpType.None => Delivery.Reliable, // treat as reliable/no-op; should generally not appear
            _ => throw new InvalidOperationException($"Unclassified RepOpType delivery policy: {type}")
        };

        /// <summary>
        /// For Unreliable ops only: returns a stable replace-key for coalescing (latest-wins) within a tick.
        /// - PositionSnapshot uses entityId.
        /// </summary>
        public static int UnreliableReplaceKey(in RepOp op) => op.Type switch
        {
            RepOpType.PositionSnapshot => op.EntityId,
            _ => 0
        };
    }
}
