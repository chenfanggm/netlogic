using System;

namespace com.aqua.netlogic.command
{
    /// <summary>
    /// Base class for all commands that can be enqueued into <see cref="ServerEngine"/>.
    /// Engine commands are authoritative: the server can accept, schedule, validate, and route them.
    /// </summary>
    public abstract class EngineCommand<TCommandType>(TCommandType type)
        where TCommandType : struct, Enum
    {
        public TCommandType Type { get; } = type;

        /// <summary>
        /// Replacement identity within a tick (per-connection).
        /// Commands with the same (Type, ReplaceKey) replace each other.
        /// </summary>
        public abstract int ReplaceKey { get; }
    }
}
