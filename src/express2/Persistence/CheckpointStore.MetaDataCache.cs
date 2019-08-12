using Neo.IO;
using OneOf;
using RocksDbSharp;
using System;

namespace Neo.Express.Backend2.Persistence
{
    internal partial class CheckpointStore
    {
        private class MetaDataCache<T> : IO.Caching.MetaDataCache<T>
            where T : class, ICloneable<T>, ISerializable, new()
        {
            // value initalized to None to indicate value hasn't been overwritten
            private OneOf<T, OneOf.Types.None> value;

            private readonly RocksDb db;
            private readonly byte[] key;
            private readonly ColumnFamilyHandle columnFamily;
            private readonly Action<T> updater;

            public OneOf<T, OneOf.Types.None> Value => value;
            public Action<T> Updater => item => value = item;

            public MetaDataCache(RocksDb db, byte[] key, ColumnFamilyHandle columnFamily, Func<T> factory = null) : base(factory)
            {
                value = new OneOf.Types.None();

                this.db = db;
                this.key = key;
                this.columnFamily = columnFamily;
            }

            public MetaDataCache(RocksDb db, byte[] key, ColumnFamilyHandle columnFamily, OneOf<T, OneOf.Types.None> value, Action<T> updater, Func<T> factory = null) : base(factory)
            {
                this.value = value;
                this.updater = updater;

                this.db = db;
                this.key = key;
                this.columnFamily = columnFamily;
            }

            protected override T TryGetInternal()
            {
                if (value.IsT0)
                    return value.AsT0;

                return db.TryGet<T>(key, columnFamily);
            }

            protected override void AddInternal(T item)
            {
                updater?.Invoke(item);
            }

            protected override void UpdateInternal(T item)
            {
                updater?.Invoke(item);
            }
        }
    }
}
