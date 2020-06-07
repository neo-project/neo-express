using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    using TrackingMap = ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;
    using WriteBatchMap = ConcurrentDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    partial class CheckpointStore
    {
        class SnapshotTracker
        {
            private readonly IReadOnlyStore store;
            private readonly byte table;
            private readonly TrackingMap trackingMap;
            private readonly WriteBatchMap writeBatch = new WriteBatchMap(ByteArrayComparer.Default);

            public SnapshotTracker(IReadOnlyStore store, byte table, TrackingMap trackingMap)
            {
                this.store = store;
                this.table = table;
                this.trackingMap = trackingMap;
            }

            public byte[]? TryGet(byte[]? key)
                => CheckpointStore.TryGet(store, table, key, trackingMap);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[] prefix)
                => CheckpointStore.Find(store, table, prefix, trackingMap);

            public void Update(byte[]? key, OneOf<byte[], OneOf.Types.None> value)
            {
                writeBatch[key ?? Array.Empty<byte>()] = value;
            }

            public void Commit(DataTracker dataTracker)
            {
                foreach (var kvp in writeBatch)
                {
                    dataTracker.Update(kvp.Key, kvp.Value);
                }
            }
        }
    }
}
