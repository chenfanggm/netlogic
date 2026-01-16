using System;
using System.Collections.Generic;

namespace Sim
{
    public readonly struct CommandBatch
    {
        public readonly int ScheduledTick;
        public readonly uint ClientCmdSeq;
        public readonly List<EngineCommand> Commands;

        public CommandBatch(int scheduledTick, uint clientCmdSeq, List<EngineCommand> commands)
        {
            ScheduledTick = scheduledTick;
            ClientCmdSeq = clientCmdSeq;
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }
    }
}
