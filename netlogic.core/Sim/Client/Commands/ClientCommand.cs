using System;

namespace Sim.Client.Command
{
    public enum ClientCommandType : byte
    {
        None = 0,
        MoveBy = 1,
        FlowFire = 2
    }

    public readonly struct ClientCommand
    {
        public readonly ClientCommandType Type;

        /// <summary>
        /// Used by FlowFire. Interpreted as a GameFlowIntent byte.
        /// </summary>
        public readonly byte Trigger;

        public readonly int EntityId;

        public readonly int Dx;
        public readonly int Dy;

        public ClientCommand(ClientCommandType type, byte trigger, int entityId, int dx, int dy)
        {
            Type = type;
            Trigger = trigger;
            EntityId = entityId;
            Dx = dx;
            Dy = dy;
        }

        public static ClientCommand MoveBy(int entityId, int dx, int dy)
        {
            return new ClientCommand(ClientCommandType.MoveBy, trigger: 0, entityId, dx, dy);
        }

        public static ClientCommand FlowFire(byte trigger)
        {
            return new ClientCommand(ClientCommandType.FlowFire, trigger, entityId: 0, dx: 0, dy: 0);
        }
    }
}
