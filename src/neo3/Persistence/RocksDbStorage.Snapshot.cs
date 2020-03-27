using System;
using System.Collections.Generic;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    partial class RocksDbStorage
    {
        class Snapshot : ISnapshot
        {
            private readonly RocksDbStorage storage;
            private readonly RocksDbSharp.Snapshot snapshot;
            private readonly ReadOptions readOptions;
            private readonly WriteBatch writeBatch;

            public Snapshot(RocksDbStorage storage)
            {
                this.storage = storage;
                snapshot = storage.db.CreateSnapshot();
                readOptions = new ReadOptions()
                    .SetSnapshot(snapshot)
                    .SetFillCache(false);
                writeBatch = new WriteBatch();
            }

            public void Dispose()
            {
                snapshot.Dispose();
                writeBatch.Dispose();
            }

            public void Commit()
            {
                storage.db.Write(writeBatch);
            }

            public void Delete(byte table, byte[]? key)
            {
                writeBatch.Delete(key ?? Array.Empty<byte>(), storage.GetColumnFamily(table));
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
            {
                using var iterator = storage.db.NewIterator(storage.GetColumnFamily(table), readOptions);
                for (iterator.Seek(prefix); iterator.Valid(); iterator.Next())
                {
                    var key = iterator.Key();
                    if (key.Length < prefix.Length) break;
                    if (!key.AsSpan().StartsWith(prefix)) break;
                    yield return (key, iterator.Value());
                }
            }

            public void Put(byte table, byte[]? key, byte[] value)
            {
                writeBatch.Put(key ?? Array.Empty<byte>(), value, storage.GetColumnFamily(table));
            }

            public byte[] TryGet(byte table, byte[]? key)
            {
                return storage.db.Get(key ?? Array.Empty<byte>(), storage.GetColumnFamily(table), readOptions);
            }
        }
    }
}
