using Neo.IO;
using RocksDbSharp;
using System;

namespace NeoExpress.Neo2Backend.Persistence
{
    internal partial class RocksDbStore
    {
        private class MetaDataCache<T> : Neo.IO.Caching.MetaDataCache<T>
            where T : class, ICloneable<T>, ISerializable, new()
        {
            private readonly RocksDb db;
            private readonly byte[] key;
            private readonly ColumnFamilyHandle familyHandle;
            private readonly ReadOptions readOptions;
            private readonly WriteBatch writeBatch;

            public MetaDataCache(RocksDb db, byte[] key, ColumnFamilyHandle familyHandle, ReadOptions readOptions, WriteBatch writeBatch, Func<T> factory = null)
                : base(factory)
            {
                this.db = db;
                this.key = key;
                this.familyHandle = familyHandle;
                this.readOptions = readOptions;
                this.writeBatch = writeBatch;
            }

            protected override T TryGetInternal()
            {
                return db.TryGet<T>(key, familyHandle, readOptions);
            }

            protected override void AddInternal(T item)
            {
                UpdateInternal(item);
            }

            protected override void UpdateInternal(T item)
            {
                writeBatch?.Put(key, item.ToArray(), familyHandle);
            }
        }
    }
}
