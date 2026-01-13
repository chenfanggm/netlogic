using System;
using System.Collections.Generic;

namespace Net
{
    public enum MsgType : byte
    {
        Hello = 1,
        Welcome = 2,
        CommandBatch = 3,
        Snapshot = 4,
        Delta = 5,
        Ack = 6,
        Ping = 7,
        Pong = 8
    }

    public interface IMessage
    {
        MsgType Type { get; }
    }

    public sealed record HelloMsg(string PlayerName) : IMessage
    {
        public MsgType Type => MsgType.Hello;
    }

    public sealed record WelcomeMsg(int PlayerId, int ServerTick, int TickRateHz) : IMessage
    {
        public MsgType Type => MsgType.Welcome;
    }

    public enum CommandType : byte
    {
        Move = 1,
        Spawn = 2,
        Damage = 3,
    }

    public readonly record struct Command(
        int PlayerId,
        int TargetTick,
        CommandType Type,
        int A,
        int B
    );

    public sealed record CommandBatchMsg(uint ClientSeq, int PlayerId, List<Command> Commands) : IMessage
    {
        public MsgType Type => MsgType.CommandBatch;
    }

    public sealed record AckMsg(uint AckClientSeq) : IMessage
    {
        public MsgType Type => MsgType.Ack;
    }

    public readonly record struct EntityState(int Id, int X, int Y, int Hp);

    public sealed record SnapshotMsg(int Tick, EntityState[] Entities) : IMessage
    {
        public MsgType Type => MsgType.Snapshot;
    }

    public sealed record DeltaMsg(int Tick, EntityState[] Changed, int[] RemovedIds, EntityState[] Added) : IMessage
    {
        public MsgType Type => MsgType.Delta;
    }
}
