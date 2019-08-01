using Neo.IO;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace Neo.Express.Persistence
{
    internal partial class RocksDbStore
    {
        private class DataCache<TKey, TValue> : IO.Caching.DataCache<TKey, TValue>
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            private readonly RocksDb db;
            private readonly ColumnFamilyHandle familyHandle;
            private readonly ReadOptions readOptions;
            private readonly WriteBatch writeBatch;

            public DataCache(RocksDb db, ColumnFamilyHandle familyHandle, ReadOptions readOptions, WriteBatch writeBatch)
            {
                this.db = db;
                this.familyHandle = familyHandle;
                this.readOptions = readOptions;
                this.writeBatch = writeBatch;
            }

            protected override IEnumerable<KeyValuePair<TKey, TValue>> FindInternal(byte[] key_prefix)
            {
                return db.Find<TKey, TValue>(key_prefix, familyHandle, readOptions);
            }

            protected override TValue GetInternal(TKey key)
            {
                return db.Get<TKey, TValue>(key, familyHandle, readOptions);
            }

            protected override TValue TryGetInternal(TKey key)
            {
                return db.TryGet<TKey, TValue>(key, familyHandle, readOptions);
            }

            protected override void AddInternal(TKey key, TValue value)
            {
                UpdateInternal(key, value);
            }

            protected override void UpdateInternal(TKey key, TValue value)
            {
                writeBatch?.Put(key.ToArray(), value.ToArray(), familyHandle);
            }

            public override void DeleteInternal(TKey key)
            {
                writeBatch?.Delete(key.ToArray(), familyHandle);
            }
        }
    }
}
