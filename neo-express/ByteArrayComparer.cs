using System;
using System.Collections.Generic;

namespace Neo.Express
{
    internal class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            return x.AsSpan().SequenceEqual(y.AsSpan());
        }

        public int GetHashCode(byte[] obj)
        {
            int hash = 0;
            for (int i = 0; i < obj.Length; i++)
            {
                hash = HashCode.Combine(hash, i, obj[i]);
            }
            return hash;
        }
    }
}
