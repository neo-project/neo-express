using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Wrappers;
using Neo.Ledger;
using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Express.Persistence
{
    internal class DevMetaDataCache<T> : MetaDataCache<T>
        where T : class, ICloneable<T>, ISerializable, new()
    {
        private readonly RocksDb db;
        private readonly byte[] key;
        private readonly ColumnFamilyHandle familyHandle;
        private readonly ReadOptions readOptions;
        private readonly WriteBatch writeBatch;

        public DevMetaDataCache(RocksDb db, byte[] key, ColumnFamilyHandle familyHandle, ReadOptions readOptions, WriteBatch writeBatch, Func<T> factory = null)
            : base(factory)
        {
            this.db = db;
            this.key = key;
            this.familyHandle = familyHandle;
            this.readOptions = readOptions;
            this.writeBatch = writeBatch;
        }

        protected override void UpdateInternal(T item)
        {
            writeBatch?.Put(key, item.ToArray(), familyHandle);
        }

        protected override void AddInternal(T item)
        {
            UpdateInternal(item);
        }

        protected override T TryGetInternal()
        {
            var value = db.Get(key, familyHandle, readOptions);
            return (value?.Length > 0)
                ? value.AsSerializable<T>()
                : null;
        }
    }
}
