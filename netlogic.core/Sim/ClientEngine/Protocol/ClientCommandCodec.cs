using System;
using LiteNetLib.Utils;
using com.aqua.netlogic.net;

namespace com.aqua.netlogic.sim.clientengine.protocol
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

            if (cmd.Type == ClientCommandType.FlowFire)
            {
                OpsWriter.WriteFlowFire(writer, cmd.Trigger, cmd.Param0);
                return;
            }

            throw new InvalidOperationException("Unsupported ClientCommandType: " + cmd.Type);
        }
    }
}
