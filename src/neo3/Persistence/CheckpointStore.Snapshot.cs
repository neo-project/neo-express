using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;

namespace NeoExpress.Neo3.Persistence
{
    partial class CheckpointStore
    {
        class Snapshot : ISnapshot
        {
            private readonly ConcurrentDictionary<byte, SnapshotTracker> snapshotTrackers = new ConcurrentDictionary<byte, SnapshotTracker>();

            public Snapshot(ConcurrentDictionary<byte, DataTracker> dataTrackers)
            {
                foreach (var kvp in dataTrackers)
                {
                    snapshotTrackers.TryAdd(kvp.Key, kvp.Value.GetSnapshot());
                }
            }

            SnapshotTracker GetSnapshotTracker(byte table) 
                => snapshotTrackers.TryGetValue(table, out var tracker) ? tracker : throw new Exception();

            public void Commit()
            {
                foreach (var tracker in snapshotTrackers.Values)
                {
                    tracker.Commit();
                }
            }

            public void Dispose()
            {
            }

            public byte[]? TryGet(byte table, byte[]? key)
                => GetSnapshotTracker(table).TryGet(key);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
                => GetSnapshotTracker(table).Find(prefix);

            public void Put(byte table, byte[]? key, byte[] value)
            {
                GetSnapshotTracker(table).Update(key, value);
            }

            public void Delete(byte table, byte[] key)
            {
                GetSnapshotTracker(table).Update(key, CheckpointStore.NONE_INSTANCE);
            }
        }
    }
}
