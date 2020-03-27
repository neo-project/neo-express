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

            public byte[] TryGet(byte table, byte[]? key)
            {
                key ??= Array.Empty<byte>();
                return storage.db.Get(key, storage.GetColumnFamily(table), readOptions);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
            {
                return storage.db.Find(prefix, storage.GetColumnFamily(table), readOptions);
            }

            public void Put(byte table, byte[]? key, byte[] value)
            {
                key ??= Array.Empty<byte>();
                writeBatch.Put(key, value, storage.GetColumnFamily(table));
            }

            public void Delete(byte table, byte[]? key)
            {
                key ??= Array.Empty<byte>();
                writeBatch.Delete(key, storage.GetColumnFamily(table));
            }
        }
    }
}
