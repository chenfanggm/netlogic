using System.Collections.Generic;
using LiteNetLib.Utils;
using Net;

namespace Client2.Protocol
{
    public sealed class ClientOpsMsgToClientCommandConverter
    {
        public ClientOpsMsgToClientCommandConverter(int initialCapacity)
        {
            _ = initialCapacity;
        }

        /// <summary>
        /// Always returns a NEW list instance. Caller must not modify after enqueue.
        /// </summary>
        public List<ClientCommand> ConvertToNewList(ClientOpsMsg msg)
        {
            if (msg == null)
                return new List<ClientCommand>(0);

            if (msg.OpCount == 0)
                return new List<ClientCommand>(0);

            byte[] payload = msg.OpsPayload;
            if (payload == null || payload.Length == 0)
                return new List<ClientCommand>(0);

            List<ClientCommand> list = new List<ClientCommand>(msg.OpCount);

            NetDataReader reader = new NetDataReader(payload, 0, payload.Length);

            int i = 0;
            while (i < msg.OpCount)
            {
                OpType opType = OpsReader.ReadOpType(reader);
                ushort opLen = OpsReader.ReadOpLen(reader);

                if (opType == OpType.MoveBy)
                {
                    int entityId = reader.GetInt();
                    int dx = reader.GetInt();
                    int dy = reader.GetInt();

                    list.Add(ClientCommand.MoveBy(entityId, dx, dy));
                }
                else if (opType == OpType.FlowFire)
                {
                    byte trigger = reader.GetByte();
                    // padding
                    reader.GetByte();
                    reader.GetByte();
                    reader.GetByte();

                    int param0 = 0;
                    if (opLen >= 8)
                        param0 = reader.GetInt();

                    list.Add(ClientCommand.FlowFire(trigger, param0));
                }
                else
                {
                    OpsReader.SkipBytes(reader, opLen);
                }

                i++;
            }

            return list;
        }
    }
}
