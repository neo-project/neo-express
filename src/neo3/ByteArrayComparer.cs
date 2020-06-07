using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NeoExpress.Neo3
{
    class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        static Lazy<ByteArrayComparer> defaultComparer = new Lazy<ByteArrayComparer>(() => new ByteArrayComparer());
        public static ByteArrayComparer Default => defaultComparer.Value;

        public bool Equals([AllowNull] byte[] x, [AllowNull] byte[] y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;
            return x.AsSpan().SequenceEqual(y.AsSpan());
        }

        public int GetHashCode([DisallowNull] byte[] obj)
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
