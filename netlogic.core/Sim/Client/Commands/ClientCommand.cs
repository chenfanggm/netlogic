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

        // MoveBy
        public readonly int EntityId;
        public readonly int Dx;
        public readonly int Dy;

        // FlowFire
        public readonly byte Trigger;
        public readonly int Param0;

        private ClientCommand(ClientCommandType type, int entityId, int dx, int dy, byte trigger, int param0)
        {
            Type = type;
            EntityId = entityId;
            Dx = dx;
            Dy = dy;
            Trigger = trigger;
            Param0 = param0;
        }

        public static ClientCommand MoveBy(int entityId, int dx, int dy)
            => new ClientCommand(ClientCommandType.MoveBy, entityId, dx, dy, trigger: 0, param0: 0);

        public static ClientCommand FlowFire(byte trigger, int param0)
            => new ClientCommand(ClientCommandType.FlowFire, entityId: 0, dx: 0, dy: 0, trigger: trigger, param0: param0);
    }
}
