using Neo.IO;
using Neo.IO.Caching;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace Neo.Express.Persistence
{
    internal class DevDataCache<TKey, TValue> : DataCache<TKey, TValue>
        where TKey : IEquatable<TKey>, ISerializable, new()
        where TValue : class, ICloneable<TValue>, ISerializable, new()
    {
        private readonly RocksDb db;
        private readonly ColumnFamilyHandle familyHandle;
        private readonly ReadOptions readOptions;
        private readonly WriteBatch writeBatch;

        public DevDataCache(RocksDb db, ColumnFamilyHandle familyHandle, ReadOptions readOptions, WriteBatch writeBatch)
        {
            this.db = db;
            this.familyHandle = familyHandle;
            this.readOptions = readOptions;
            this.writeBatch = writeBatch;
        }

        public override void DeleteInternal(TKey key)
        {
            writeBatch?.Delete(key.ToArray(), familyHandle);
        }

        protected override void UpdateInternal(TKey key, TValue value)
        {
            writeBatch?.Put(key.ToArray(), value.ToArray(), familyHandle);
        }

        protected override void AddInternal(TKey key, TValue value)
        {
            UpdateInternal(key, value);
        }

        protected override IEnumerable<KeyValuePair<TKey, TValue>> FindInternal(byte[] key_prefix)
        {
            using (var iterator = db.NewIterator(familyHandle, readOptions))
            {
                while (true)
                {
                    iterator.Seek(key_prefix);
                    if (!iterator.Valid())
                        break;

                    var key = iterator.Key().AsSerializable<TKey>();
                    var value = iterator.Value().AsSerializable<TValue>();
                    yield return new KeyValuePair<TKey, TValue>(key, value);
                }
            }
        }

        protected override TValue GetInternal(TKey key)
        {
            return db.Get(key.ToArray(), familyHandle, readOptions)
                .AsSerializable<TValue>();
        }

        protected override TValue TryGetInternal(TKey key)
        {
            var value = db.Get(key.ToArray(), familyHandle, readOptions);
            return value?.Length > 0 
                ? value.AsSerializable<TValue>()
                : null;
        }
    }
}
