using System;
using System.Collections.Generic;
using com.aqua.netlogic.command;
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.systems.gameflowsystem.commands;
using com.aqua.netlogic.sim.systems.movementsystem.commands;

namespace com.aqua.netlogic.sim.clientengine.protocol
{
    /// <summary>
    /// Converts engine commands into client commands for network submission.
    /// </summary>
    public sealed class EngineCommandToClientCommandConverter
    {
        public static ClientCommand[] ConvertToNewArray(EngineCommand<EngineCommandType> command)
        {
            if (command == null)
                return Array.Empty<ClientCommand>();

            List<ClientCommand> list = new List<ClientCommand>(1);

            Append(list, command);

            return list.Count == 0 ? Array.Empty<ClientCommand>() : list.ToArray();
        }

        public static ClientCommand[] ConvertToNewArray(List<EngineCommand<EngineCommandType>> commands)
        {
            if (commands == null || commands.Count == 0)
                return Array.Empty<ClientCommand>();

            List<ClientCommand> list = new List<ClientCommand>(commands.Count);

            for (int i = 0; i < commands.Count; i++)
                Append(list, commands[i]);

            return list.Count == 0 ? Array.Empty<ClientCommand>() : list.ToArray();
        }

        private static void Append(List<ClientCommand> list, EngineCommand<EngineCommandType> command)
        {
            if (command == null)
                return;

            switch (command.Type)
            {
                case EngineCommandType.MoveBy:
                    if (command is MoveByEngineCommand move)
                        list.Add(ClientCommand.MoveBy(move.EntityId, move.Dx, move.Dy));
                    break;

                case EngineCommandType.FlowFire:
                    if (command is FlowIntentEngineCommand flow)
                        list.Add(ClientCommand.FlowFire((byte)flow.Intent, flow.Param0));
                    break;

                case EngineCommandType.None:
                default:
                    break;
            }
        }
    }
}
