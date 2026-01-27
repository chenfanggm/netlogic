using Sim.Game;
using Sim.Game.Flow;
using Sim.Command;
using Sim.System;

namespace Client2.Protocol
{
    /// <summary>
    /// Converts client-authored commands into authoritative engine commands.
    /// This is the seam where you can:
    /// - validate / clamp values
    /// - map client versions
    /// - translate into richer server-side commands
    /// </summary>
    public sealed class ClientCommandToEngineCommandConverter
    {
        /// <summary>
        /// Always returns a NEW list instance. Caller must not mutate after enqueue.
        /// </summary>
        public static List<EngineCommand<EngineCommandType>> ConvertToNewList(List<ClientCommand> clientCommands)
        {
            if (clientCommands == null || clientCommands.Count == 0)
                return new List<EngineCommand<EngineCommandType>>(0);

            List<EngineCommand<EngineCommandType>> list =
                new List<EngineCommand<EngineCommandType>>(clientCommands.Count);

            for (int i = 0; i < clientCommands.Count; i++)
            {
                ClientCommand c = clientCommands[i];
                switch (c.Type)
                {
                    case ClientCommandType.MoveBy:
                        list.Add(new MoveByEngineCommand(c.EntityId, c.Dx, c.Dy));
                        break;

                    case ClientCommandType.FlowFire:
                        // Flow intents are authoritative; we accept a byte and cast.
                        // Unknown values are ignored.
                        if (c.Trigger != 0)
                            list.Add(new FlowIntentEngineCommand((GameFlowIntent)c.Trigger, c.Param0));
                        break;

                    case ClientCommandType.None:
                    default:
                        // Unknown / unsupported => drop.
                        break;
                }
            }

            return list;
        }
    }
}
