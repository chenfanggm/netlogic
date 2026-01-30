using System;
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;

namespace com.aqua.netlogic.command.events
{
    /// <summary>
    /// Event payload for client-authored command submission.
    /// </summary>
    public sealed class CommandEvent
    {
        public EngineCommand<EngineCommandType> Command { get; }

        public CommandEvent(EngineCommand<EngineCommandType> command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }
    }
}
