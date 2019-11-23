using Neo.IO;
using OneOf;
using RocksDbSharp;
using System;

namespace NeoExpress.Persistence
{
    internal partial class CheckpointStore
    {
        private class MetaDataCache<T> : Neo.IO.Caching.MetaDataCache<T>
            where T : class, ICloneable<T>, ISerializable, new()
        {
            private readonly RocksDb db;
            private readonly byte[] key;
            private readonly ColumnFamilyHandle columnFamily;
            // value initalized to None to indicate value hasn't been overwritten
            private OneOf<T, OneOf.Types.None> value;
            private readonly Action<T>? updater;

            public MetaDataCache(RocksDb db, byte key, OneOf<T, OneOf.Types.None> value, Func<T>? factory = null) : base(factory)
            {
                this.db = db;
                this.key = new byte[] { key };
                columnFamily = db.GetColumnFamily(RocksDbStore.METADATA_FAMILY);
                this.value = value;
            }

            public MetaDataCache(RocksDb db, byte key, OneOf<T, OneOf.Types.None> value, Action<T> updater, Func<T>? factory = null) 
                : this(db, key, value, factory)
            {
                this.updater = updater;
            }

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member.
            protected override T? TryGetInternal()
#pragma warning restore CS8609 // Nullability of reference types in return type doesn't match overridden member.
            {
                return value.Match(v => v, _ => db.TryGet<T>(key, columnFamily));
            }

            protected override void AddInternal(T item)
            {
                UpdateInternal(item);
            }

            protected override void UpdateInternal(T item)
            {
                if (updater == null)
                    throw new InvalidOperationException();

                value = item;
                updater(item);
            }
        }
    }
}
