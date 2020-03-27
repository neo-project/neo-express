using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using OneOf;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    using TrackingDictionary = ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    partial class CheckpointStorage
    {
        class DataTracker
        {
            private readonly CheckpointStorage storage;
            private readonly ColumnFamilyHandle? columnFamily;
            private TrackingDictionary updatedValues;

            public DataTracker(CheckpointStorage storage, ColumnFamilyHandle? columnFamily)
            {
                this.storage = storage;
                this.columnFamily = columnFamily;
                this.updatedValues = TrackingDictionary.Empty.WithComparers(new ByteArrayComparer());
            }

            public SnapshotTracker GetSnapshot() => new SnapshotTracker(this, this.updatedValues);

            public byte[]? TryGet(byte[]? key, TrackingDictionary updatedValues)
            {
                key ??= Array.Empty<byte>();
                if (updatedValues.TryGetValue(key, out var value))
                {
                    return value.Match<byte[]?>(v => v, _ => null);
                }

                return columnFamily == null
                    ? null
                    : storage.db.Get(key, columnFamily, storage.readOptions);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[] prefix, TrackingDictionary updatedValues)
            {
                foreach (var kvp in updatedValues)
                {
                    if (kvp.Key.AsSpan().StartsWith(prefix) && kvp.Value.IsT0)
                    {
                        yield return (kvp.Key, kvp.Value.AsT0);
                    }
                }

                if (columnFamily != null)
                {
                    foreach (var kvp in storage.db.Find(prefix, columnFamily, storage.readOptions))
                    {
                        if (!updatedValues.ContainsKey(kvp.key))
                        {
                            yield return kvp;
                        }
                    }
                }
            }

            public byte[]? TryGet(byte[]? key)
                => TryGet(key, updatedValues);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[] prefix)
                => Find(prefix, updatedValues);

            public void Update(byte[]? key, OneOf<byte[], OneOf.Types.None> value)
            {
                key ??= Array.Empty<byte>();
                updatedValues = updatedValues.SetItem(key, value);
            }
        }
    }
}
