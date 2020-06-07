using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Persistence;
using OneOf;

namespace NeoExpress.Neo3.Persistence
{
    class NullReadOnlyStore : IReadOnlyStore
    {
        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Find(byte table, byte[]? prefix)
        {
            return Enumerable.Empty<(byte[] Key, byte[] Value)>();
        }

        byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key)
        {
            return null;
        }
    }
}
