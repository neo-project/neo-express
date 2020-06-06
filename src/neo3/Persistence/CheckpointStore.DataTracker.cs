using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;

namespace NeoExpress.Neo3.Persistence
{
    using TrackingDictionary = ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    partial class CheckpointStore
    {
        class DataTracker
        {
            private readonly IReadOnlyStore store;
            private readonly byte table;
            private TrackingDictionary updatedValues = TrackingDictionary.Empty.WithComparers(new ByteArrayComparer());

            public DataTracker(IReadOnlyStore store, byte table)
            {
                this.store = store;
                this.table = table;
            }

            public SnapshotTracker GetSnapshot() => new SnapshotTracker(this, this.updatedValues);

            public byte[]? TryGet(byte[]? key)
                => TryGet(key, updatedValues);

            public byte[]? TryGet(byte[]? key, TrackingDictionary updatedValues)
            {
                if (updatedValues.TryGetValue(key ?? Array.Empty<byte>(), out var value))
                {
                    return value.Match<byte[]?>(v => v, _ => null);
                }

                return store.TryGet(table, key);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? prefix)
                => Find(prefix, updatedValues);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? prefix, TrackingDictionary updatedValues)
            {
                foreach (var kvp in updatedValues)
                {
                    if (kvp.Key.AsSpan().StartsWith(prefix ?? Array.Empty<byte>()) && kvp.Value.IsT0)
                    {
                        yield return (kvp.Key, kvp.Value.AsT0);
                    }
                }

                foreach (var kvp in store.Find(table, prefix))
                {
                    if (!updatedValues.ContainsKey(kvp.Key))
                    {
                        yield return kvp;
                    }
                }
            }

            public void Update(byte[]? key, OneOf<byte[], OneOf.Types.None> value)
            {
                updatedValues = updatedValues.SetItem(key ?? Array.Empty<byte>(), value);
            }
        }
    }
}
