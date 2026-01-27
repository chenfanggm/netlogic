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

        public readonly int Param0;

        public readonly int EntityId;

        public readonly int Dx;
        public readonly int Dy;

        public ClientCommand(ClientCommandType type, byte trigger, int param0, int entityId, int dx, int dy)
        {
            Type = type;
            Trigger = trigger;
            Param0 = param0;
            EntityId = entityId;
            Dx = dx;
            Dy = dy;
        }

        public static ClientCommand MoveBy(int entityId, int dx, int dy)
            => new ClientCommand(ClientCommandType.MoveBy, trigger: 0, param0: 0, entityId, dx, dy);

        public static ClientCommand FlowFire(byte trigger, int param0)
            => new ClientCommand(ClientCommandType.FlowFire, trigger, param0, entityId: 0, dx: 0, dy: 0);
    }
}
