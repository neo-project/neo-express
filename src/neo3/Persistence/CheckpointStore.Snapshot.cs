using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;

namespace NeoExpress.Neo3.Persistence
{
    using TrackingMap = ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    partial class CheckpointStore
    {
        class Snapshot : ISnapshot
        {
            private readonly CheckpointStore store;
            private readonly ConcurrentDictionary<byte, SnapshotTracker> snapshotTrackers = new ConcurrentDictionary<byte, SnapshotTracker>();

            public Snapshot(CheckpointStore store)
            {
                this.store = store;
                foreach (var kvp in store.dataTrackers)
                {
                    snapshotTrackers.TryAdd(kvp.Key, kvp.Value.GetSnapshot());
                }
            }

            public void Dispose()
            {
            }

            SnapshotTracker GetSnapshotTracker(byte table) 
                => snapshotTrackers.GetOrAdd(table, t => new SnapshotTracker(store.store, t, TrackingMap.Empty)); 

            public byte[]? TryGet(byte table, byte[]? key)
                => GetSnapshotTracker(table).TryGet(key);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
                => GetSnapshotTracker(table).Find(prefix);

            public void Put(byte table, byte[]? key, byte[] value) 
                => GetSnapshotTracker(table).Update(key, value);

            public void Delete(byte table, byte[] key) 
                => GetSnapshotTracker(table).Update(key, CheckpointStore.NONE_INSTANCE);

            public void Commit()
            {
                foreach (var kvp in snapshotTrackers)
                {
                    var tracker = store.GetDataTracker(kvp.Key);
                    kvp.Value.Commit(tracker);
                }
            }
        }
    }
}
