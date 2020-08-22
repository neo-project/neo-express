using Neo.Trie;
using OneOf;
using RocksDbSharp;
using System.Collections.Immutable;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class CheckpointStore
    {
        private class KVTracker 
        {
            private readonly RocksDb db;
            private readonly ColumnFamilyHandle columnFamily;

            // dictionary value of None indicates the key has been deleted
            private ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>> updatedValues =
                ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>.Empty
                    .WithComparers(new ByteArrayComparer());

            public KVTracker(RocksDb db, string familyName)
            {
                this.db = db;
                this.columnFamily = db.GetColumnFamily(familyName);
            }

            public IKVStore GetSnapshot() => new KVStore(this, updatedValues);

            public byte[]? TryGet(byte[] key, ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>? snapshotValues)
            {
                snapshotValues ??= updatedValues;
                if (snapshotValues.TryGetValue(key, out var value))
                {
                    return value.Match<byte[]?>(v => v, _ => null);
                }

                return db.Get(key, columnFamily);
            }

            public void Update(byte[] key, OneOf<byte[], OneOf.Types.None> value)
                => updatedValues = updatedValues.SetItem(key, value);
        }
    }
}
