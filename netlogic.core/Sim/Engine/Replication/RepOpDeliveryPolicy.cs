using System;

namespace Sim.Engine
{
    internal static class RepOpDeliveryPolicy
    {
        internal enum Delivery : byte
        {
            Reliable = 0,
            Sample = 1,
        }

        public static Delivery DeliveryOf(RepOpType type) => type switch
        {
            // Sample lane
            RepOpType.PositionAt => Delivery.Sample,

            // Reliable lane (flow/UI control)
            RepOpType.FlowFire => Delivery.Reliable,
            RepOpType.FlowSnapshot => Delivery.Reliable,

            RepOpType.None => Delivery.Reliable, // treat as reliable/no-op; should generally not appear
            _ => throw new InvalidOperationException($"Unclassified RepOpType delivery policy: {type}")
        };

        /// <summary>
        /// For Sample ops only: returns a stable replace-key for coalescing (latest-wins) within a tick.
        /// - PositionAt uses entityId stored in A.
        /// </summary>
        public static int SampleReplaceKey(in RepOp op) => op.Type switch
        {
            RepOpType.PositionAt => op.A, // entityId
            _ => 0
        };
    }
}
