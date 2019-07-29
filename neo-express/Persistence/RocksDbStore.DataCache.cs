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
                using (var iterator = db.NewIterator(familyHandle, readOptions))
                {
                    iterator.Seek(key_prefix);
                    while (iterator.Valid())
                    {
                        yield return new KeyValuePair<TKey, TValue>(
                            iterator.Key().AsSerializable<TKey>(),
                            iterator.Value().AsSerializable<TValue>());
                        iterator.Next();
                    }
                }
            }

            protected override TValue GetInternal(TKey key)
            {
                var value = TryGetInternal(key);
                if (value == null)
                {
                    throw new Exception("not found");
                }
                return value;
            }

            protected override TValue TryGetInternal(TKey key)
            {
                var value = db.Get(key.ToArray(), familyHandle, readOptions);
                return value?.Length > 0
                    ? value.AsSerializable<TValue>()
                    : null;
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
