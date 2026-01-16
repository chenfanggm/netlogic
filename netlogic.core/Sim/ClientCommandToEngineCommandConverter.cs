using System.Collections.Generic;

namespace Sim
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
        public List<EngineCommand> ConvertToNewList(List<ClientCommand> clientCommands)
        {
            if (clientCommands == null || clientCommands.Count == 0)
                return new List<EngineCommand>(0);

            List<EngineCommand> list = new List<EngineCommand>(clientCommands.Count);

            for (int i = 0; i < clientCommands.Count; i++)
            {
                ClientCommand c = clientCommands[i];
                switch (c.Type)
                {
                    case ClientCommandType.MoveBy:
                        list.Add(new MoveByEngineCommand(c.EntityId, c.Dx, c.Dy));
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
