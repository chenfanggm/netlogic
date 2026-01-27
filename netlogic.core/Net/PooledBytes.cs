using System;
using System.Buffers;
using LiteNetLib.Utils;

namespace Net
{
    internal static class PooledBytes
    {
        public static byte[] RentCopy(NetDataWriter w, out int len)
        {
            len = w.Length;
            if (len <= 0) return Array.Empty<byte>();

            byte[] buf = ArrayPool<byte>.Shared.Rent(len);
            Buffer.BlockCopy(w.Data, 0, buf, 0, len);
            return buf;
        }

        public static byte[] RentCopy(byte[] src, int len)
        {
            if (len <= 0) return Array.Empty<byte>();
            byte[] buf = ArrayPool<byte>.Shared.Rent(len);
            Buffer.BlockCopy(src, 0, buf, 0, len);
            return buf;
        }
    }
}
