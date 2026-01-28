using System;
using System.Collections.Generic;
using MemoryPack;

namespace com.aqua.netlogic.net
{
    /// <summary>
    /// Message type identifiers for network protocol.
    /// </summary>
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

    /// <summary>
    /// Base interface for all network messages.
    /// </summary>
    public interface IMessage
    {
        MsgType Type { get; }
    }

    /// <summary>
    /// Client connection request message sent to server.
    /// </summary>
    public sealed record HelloMsg(ushort ProtocolVersion, string PlayerName) : IMessage
    {
        public MsgType Type => MsgType.Hello;
    }

    /// <summary>
    /// Server welcome message sent to client upon connection.
    /// </summary>
    public sealed record WelcomeMsg(ushort ProtocolVersion, int PlayerId, int ServerTick, int TickRateHz) : IMessage
    {
        public MsgType Type => MsgType.Welcome;
    }

    /// <summary>
    /// Command type identifiers for game actions.
    /// </summary>
    public enum CommandType : byte
    {
        Move = 1,
        Spawn = 2,
        Damage = 3,
    }

    /// <summary>
    /// A game command scheduled for execution at a specific server tick.
    /// </summary>
    public readonly record struct Command(
        int PlayerId,
        int TargetTick,
        CommandType Type,
        int A,
        int B
    );

    /// <summary>
    /// Batch of commands sent from client to server with sequence number for reliability.
    /// </summary>
    public sealed record CommandBatchMsg(uint ClientSeq, int PlayerId, List<Command> Commands) : IMessage
    {
        public MsgType Type => MsgType.CommandBatch;
    }

    /// <summary>
    /// Acknowledgment message sent from server to client to confirm command batch receipt.
    /// </summary>
    public sealed record AckMsg(uint AckClientSeq) : IMessage
    {
        public MsgType Type => MsgType.Ack;
    }

    /// <summary>
    /// Snapshot of an entity's state at a specific tick.
    /// </summary>
    [MemoryPackable]
    public readonly partial record struct EntityState(int Id, int X, int Y, int Hp);

    /// <summary>
    /// Full snapshot of all entities at a specific server tick.
    /// </summary>
    public sealed record SnapshotMsg(int Tick, EntityState[] Entities) : IMessage
    {
        public MsgType Type => MsgType.Snapshot;
    }

    /// <summary>
    /// Delta update message containing only changed entities since last snapshot.
    /// </summary>
    public sealed record DeltaMsg(int Tick, EntityState[] Changed, int[] RemovedIds, EntityState[] Added) : IMessage
    {
        public MsgType Type => MsgType.Delta;
    }
}
