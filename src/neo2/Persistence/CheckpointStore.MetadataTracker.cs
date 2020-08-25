using Neo.IO;
using OneOf;
using RocksDbSharp;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class CheckpointStore
    {
        class MetadataTracker<T>
            where T : class, ICloneable<T>, ISerializable, new()
        {
            private readonly RocksDb db;
            private readonly byte[] key;
            private readonly ColumnFamilyHandle columnFamily;
            // tracker value of None indicates value hasn't been overwritten
            private OneOf<T, OneOf.Types.None> updatedValue = NONE_INSTANCE;

            public MetadataTracker(RocksDb db, byte key, ColumnFamilyHandle columnFamily)
            {
                this.db = db;
                this.key = new byte[] { key };
                this.columnFamily = columnFamily;
            }

            public Neo.IO.Caching.MetaDataCache<T> GetCache()
                => new MetaDataCache<T>(this);

            public Neo.IO.Caching.MetaDataCache<T> GetSnapshot()
                => new MetaDataCache<T>(this, updatedValue, Update);

            public T? TryGet(OneOf<T, OneOf.Types.None>? snapshotValue)
            {
                snapshotValue ??= updatedValue;
                return snapshotValue.Value.Match(
                    v => v,
                    _ => db.TryGet<T>(key, columnFamily));
            }

            public void Update(T value) => updatedValue = value;
        }
    }
}
