using System;
using LiteNetLib.Utils;
using com.aqua.netlogic.net;
using com.aqua.netlogic.sim.game.snapshot;
using com.aqua.netlogic.sim.replication;

namespace com.aqua.netlogic.sim.networkserver.protocol
{
    /// <summary>
    /// ServerMessageEncoder = turns ServerEngine outputs (snapshot + RepOps) into:
    /// - BaselineMsg
    /// - ServerOpsMsg payloads (by writing ops into a NetDataWriter)
    ///
    /// NOTE:
    /// - This class does NOT own transport or reliability streams.
    /// - NetworkServer owns ack/replay and packetization.
    /// </summary>
    public sealed class ServerMessageEncoder
    {
        public NetDataWriter Writer { get; }

        public ServerMessageEncoder(NetDataWriter? writer = null)
        {
            Writer = writer ?? new NetDataWriter();
        }

        public static BaselineMsg BuildBaseline(ServerModelSnapshot snap)
        {
            return BaselineBuilder.Build(snap);
        }

        public void EncodeUnreliablePositionSnapshotsToWriter(ReadOnlySpan<RepOp> ops, out ushort opCount)
        {
            Writer.Reset();
            opCount = 0;

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];
                if (op.Type == RepOpType.PositionSnapshot)
                {
                    OpsWriter.WritePositionSnapshot(Writer, op.EntityId, op.X, op.Y);
                    opCount++;
                }
            }
        }

        public void EncodeReliableRepOpsToWriter(ReadOnlySpan<RepOp> ops, out ushort opCount)
        {
            Writer.Reset();
            opCount = 0;

            for (int i = 0; i < ops.Length; i++)
            {
                RepOp op = ops[i];

                switch (op.Type)
                {
                    case RepOpType.EntitySpawned:
                        OpsWriter.WriteEntitySpawned(Writer, op.EntityId, op.X, op.Y, op.Hp);
                        opCount++;
                        break;

                    case RepOpType.EntityDestroyed:
                        OpsWriter.WriteEntityDestroyed(Writer, op.EntityId);
                        opCount++;
                        break;

                    default:
                        break;
                }
            }
        }

        public ServerOpsMsg BuildUnreliableServerOpsMsg(int serverTick, uint worldHash, uint serverSeq, ReadOnlySpan<RepOp> ops)
        {
            EncodeUnreliablePositionSnapshotsToWriter(ops, out ushort opCount);

            byte[] opsPayload = (opCount == 0) ? Array.Empty<byte>() : Writer.CopyData();

            return new ServerOpsMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                serverTick,
                serverSeq,
                worldHash,
                opCount,
                opsPayload);
        }
    }
}
