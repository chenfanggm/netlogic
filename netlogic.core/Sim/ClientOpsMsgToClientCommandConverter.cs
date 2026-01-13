using System;
using System.Collections.Generic;
using LiteNetLib.Utils;
using Net;

namespace Sim
{
    /// <summary>
    /// Converts wire payload (ClientOpsMsg) into engine commands (ClientCommand list).
    /// Keeps parsing logic in one place.
    /// </summary>
    public sealed class ClientOpsMsgToClientCommandConverter
    {
        private readonly List<ClientCommand> _buffer;

        public ClientOpsMsgToClientCommandConverter(int capacity)
        {
            _buffer = new List<ClientCommand>(capacity);
        }

        public List<ClientCommand> Convert(ClientOpsMsg msg)
        {
            _buffer.Clear();

            if (msg == null)
                return _buffer;

            if (msg.OpCount == 0)
                return _buffer;

            byte[] payload = msg.OpsPayload;
            if (payload == null || payload.Length == 0)
                return _buffer;

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

                    _buffer.Add(ClientCommand.MoveBy(entityId, dx, dy));
                }
                else
                {
                    OpsReader.SkipBytes(reader, opLen);
                }

                i++;
            }

            return _buffer;
        }
    }
}
