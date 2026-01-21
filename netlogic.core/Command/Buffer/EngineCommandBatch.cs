using System;
using System.Collections.Generic;

namespace Sim
{
    /// <summary>
    /// Bundles a scheduled tick, client command sequence, and command list for processing.
    /// </summary>
    public readonly struct EngineCommandBatch<TCommandType>(
        int scheduledTick,
        uint clientCmdSeq,
        List<EngineCommand<TCommandType>> commands)
        where TCommandType : struct, Enum
    {
        public readonly int ScheduledTick = scheduledTick;
        public readonly uint ClientCmdSeq = clientCmdSeq;
        public readonly List<EngineCommand<TCommandType>> Commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }
}
