using System;
using System.Collections.Generic;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    static class RocksDbExtensions
    {
        public static IEnumerable<(byte[] key, byte[] value)> Find(this RocksDb db, byte[]? prefix, ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null)
        {
            prefix ??= Array.Empty<byte>();
            using var iterator = db.NewIterator(columnFamily, readOptions);
            for (iterator.Seek(prefix); iterator.Valid(); iterator.Next())
            {
                var key = iterator.Key();
                if (key.Length < prefix.Length) break;
                if (!key.AsSpan().StartsWith(prefix)) break;
                yield return (key, iterator.Value());
            }
        }
    }
}
