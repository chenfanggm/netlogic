using System;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.clientengine.ops
{
    /// <summary>
    /// Fast handler table: RepOpType -> IClientRepOpHandler.
    /// </summary>
    public sealed class ClientRepOpHandlers
    {
        private readonly IClientRepOpHandler?[] _handlers;

        public ClientRepOpHandlers()
        {
            int max = 0;
            foreach (RepOpType t in Enum.GetValues(typeof(RepOpType)))
            {
                int v = (int)t;
                if (v > max)
                    max = v;
            }

            _handlers = new IClientRepOpHandler?[max + 1];
        }

        public void Register(RepOpType type, IClientRepOpHandler handler)
        {
            int idx = (int)type;
            if ((uint)idx >= (uint)_handlers.Length)
                throw new ArgumentOutOfRangeException(nameof(type), type, "RepOpType out of range");

            if (_handlers[idx] != null)
                throw new InvalidOperationException($"Client RepOp handler already registered: {type}");

            _handlers[idx] = handler;
        }

        public IClientRepOpHandler Get(RepOpType type)
        {
            int idx = (int)type;
            if ((uint)idx >= (uint)_handlers.Length)
                throw new ArgumentOutOfRangeException(nameof(type), type, "RepOpType out of range");

            IClientRepOpHandler? h = _handlers[idx];
            if (h == null)
                throw new InvalidOperationException($"No client RepOp handler registered for {type}");

            return h;
        }
    }
}
