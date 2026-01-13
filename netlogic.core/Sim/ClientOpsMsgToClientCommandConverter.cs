using System;
using LiteNetLib.Utils;
using Net;

namespace Sim
{
    public sealed class ClientOpsMsgToClientCommandConverter
    {
        private ClientCommand[] _scratch;

        public ClientOpsMsgToClientCommandConverter(int initialCapacity)
        {
            if (initialCapacity < 1)
                initialCapacity = 1;

            _scratch = new ClientCommand[initialCapacity];
        }

        public ClientCommand[] Convert(ClientOpsMsg msg, out int commandCount)
        {
            commandCount = 0;

            if (msg == null)
                return Array.Empty<ClientCommand>();

            if (msg.OpCount == 0)
                return Array.Empty<ClientCommand>();

            byte[] payload = msg.OpsPayload;
            if (payload == null || payload.Length == 0)
                return Array.Empty<ClientCommand>();

            EnsureCapacity(msg.OpCount);

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

                    _scratch[commandCount] = ClientCommand.MoveBy(entityId, dx, dy);
                    commandCount++;
                }
                else
                {
                    OpsReader.SkipBytes(reader, opLen);
                }

                i++;
            }

            if (commandCount == 0)
                return Array.Empty<ClientCommand>();

            ClientCommand[] result = new ClientCommand[commandCount];

            int k = 0;
            while (k < commandCount)
            {
                result[k] = _scratch[k];
                k++;
            }

            return result;
        }

        private void EnsureCapacity(int needed)
        {
            if (_scratch.Length >= needed)
                return;

            int newSize = _scratch.Length;
            while (newSize < needed)
                newSize = newSize * 2;

            _scratch = new ClientCommand[newSize];
        }
    }
}
