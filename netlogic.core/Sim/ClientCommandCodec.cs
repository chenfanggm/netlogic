using System;
using System.Collections.Generic;
using LiteNetLib.Utils;
using Net;

namespace Sim
{
    public static class ClientCommandCodec
    {
        public static void EncodeToOps(NetDataWriter writer, ClientCommand cmd)
        {
            if (cmd.Type == ClientCommandType.MoveBy)
            {
                OpsWriter.WriteMoveBy(writer, cmd.EntityId, cmd.Dx, cmd.Dy);
                return;
            }

            throw new InvalidOperationException("Unsupported command type: " + cmd.Type);
        }

        public static void DecodeFromOpsPayload(byte[] opsPayload, int offset, int length, ushort opCount, List<ClientCommand> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            NetDataReader reader = new NetDataReader(opsPayload, offset, length);

            int i = 0;
            while (i < opCount)
            {
                OpType opType = OpsReader.ReadOpType(reader);
                ushort opLen = OpsReader.ReadOpLen(reader);

                if (opType == OpType.MoveBy)
                {
                    int entityId = reader.GetInt();
                    int dx = reader.GetInt();
                    int dy = reader.GetInt();

                    output.Add(ClientCommand.MoveBy(entityId, dx, dy));
                }
                else
                {
                    OpsReader.SkipBytes(reader, opLen);
                }

                i++;
            }
        }
    }
}
