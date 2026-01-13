using System;

namespace Sim
{
    public enum ClientCommandType : byte
    {
        None = 0,
        MoveBy = 1
    }

    public readonly struct ClientCommand
    {
        public readonly ClientCommandType Type;

        public readonly int EntityId;

        public readonly int Dx;
        public readonly int Dy;

        public ClientCommand(ClientCommandType type, int entityId, int dx, int dy)
        {
            Type = type;
            EntityId = entityId;
            Dx = dx;
            Dy = dy;
        }

        public static ClientCommand MoveBy(int entityId, int dx, int dy)
        {
            return new ClientCommand(ClientCommandType.MoveBy, entityId, dx, dy);
        }
    }
}
