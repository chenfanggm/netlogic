using System;

namespace com.aqua.netlogic.sim.utils.hashing
{
    /// <summary>
    /// Stable, deterministic 32-bit FNV-1a hash builder.
    /// Use this for authoritative state hashing (desync detection).
    /// </summary>
    internal struct Fnv1a32
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;

        private uint _h;

        public static Fnv1a32 Start() => new Fnv1a32 { _h = OffsetBasis };

        public uint Finish() => _h;

        public void Add(int v) => Add(unchecked((uint)v));

        public void Add(uint v)
        {
            // Feed 4 bytes little-endian.
            AddByte((byte)(v));
            AddByte((byte)(v >> 8));
            AddByte((byte)(v >> 16));
            AddByte((byte)(v >> 24));
        }

        public void Add(bool v) => AddByte(v ? (byte)1 : (byte)0);

        public void AddByte(byte b)
        {
            _h ^= b;
            _h *= Prime;
        }
    }
}
