using MemoryPack;

namespace com.aqua.netlogic.net.wirestate
{
    [MemoryPackable]
    public readonly partial struct WireEntityState
    {
        public readonly int Id;
        public readonly int X;
        public readonly int Y;
        public readonly int Hp;

        public WireEntityState(int id, int x, int y, int hp)
        {
            Id = id;
            X = x;
            Y = y;
            Hp = hp;
        }
    }
}
