using System;
using System.Collections.Generic;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    partial class RocksDbStore
    {
        class Snapshot : ISnapshot
        {
            private readonly RocksDbStore store;
            private readonly RocksDbSharp.Snapshot snapshot;
            private readonly ReadOptions readOptions;
            private readonly WriteBatch writeBatch;

            public Snapshot(RocksDbStore store)
            {
                this.store = store;
                snapshot = store.db.CreateSnapshot();
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
                store.db.Write(writeBatch);
            }

            public byte[] TryGet(byte table, byte[]? key)
            {
                return store.db.Get(key ?? Array.Empty<byte>(), store.GetColumnFamily(table), readOptions);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[]? prefix)
            {
                return RocksDbStore.Find(store.db, prefix, store.GetColumnFamily(table), readOptions);
            }

            public void Put(byte table, byte[]? key, byte[] value)
            {
                writeBatch.Put(key ?? Array.Empty<byte>(), value, store.GetColumnFamily(table));
            }

            public void Delete(byte table, byte[]? key)
            {
                writeBatch.Delete(key ?? Array.Empty<byte>(), store.GetColumnFamily(table));
            }
        }
    }
}
