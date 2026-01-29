using com.aqua.netlogic.sim.game.snapshot;

namespace com.aqua.netlogic.sim.serverengine
{
    /// <summary>
    /// Minimal continuous state for interpolation.
    /// </summary>
    public readonly struct SampleEntityPos(int entityId, int x, int y, int hp)
    {
        public readonly int EntityId = entityId;
        public readonly int X = x;
        public readonly int Y = y;
        public readonly int Hp = hp;
    }

    /// <summary>
    /// Domain-level discrete op types (NOT wire OpType).
    /// Expand over time: container ops, entity spawn/despawn, status changes, etc.
    /// </summary>
    public enum EngineOpType : byte
    {
        None = 0,

        // Examples for future use:
        // MoveCard = 10,
        // RemoveEntity = 20,
        // SpawnEntity = 21,
    }

    /// <summary>
    /// Domain-level discrete op (NOT byte[] payload).
    /// Keep this deterministic and codec-friendly.
    /// </summary>
    public readonly struct EngineOp(EngineOpType type, int a = 0, int b = 0, int c = 0, int d = 0)
    {
        public readonly EngineOpType Type = type;

        // Generic payload fields (keep small; add specialized structs later if needed).
        public readonly int A = a;
        public readonly int B = b;
        public readonly int C = c;
        public readonly int D = d;
    }
}
