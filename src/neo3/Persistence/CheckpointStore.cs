using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;

namespace NeoExpress.Neo3.Persistence
{
    partial class CheckpointStore : IStore
    {
        private readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();

        private readonly IReadOnlyStore store;
        private readonly ConcurrentDictionary<byte, DataTracker> dataTrackers = new ConcurrentDictionary<byte, DataTracker>();

        public CheckpointStore(IReadOnlyStore store)
        {
            this.store = store;
        }

        public void Dispose()
        {
            if (store is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        DataTracker GetDataTracker(byte table) 
            => dataTrackers.GetOrAdd(table, _ => new DataTracker(store, table));

        byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key)
            => GetDataTracker(table).TryGet(key);

        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Find(byte table, byte[]? prefix)
            => GetDataTracker(table).Find(prefix);

        void IStore.Put(byte table, byte[]? key, byte[] value) 
            => GetDataTracker(table).Update(key, value);

        void IStore.Delete(byte table, byte[]? key)
            => GetDataTracker(table).Update(key, NONE_INSTANCE);

        ISnapshot IStore.GetSnapshot() => new Snapshot(dataTrackers);
    }
}
