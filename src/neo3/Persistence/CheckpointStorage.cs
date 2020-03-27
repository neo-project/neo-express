using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo.Persistence;
using OneOf;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    using TrackingDictionary = ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    partial class CheckpointStorage : IStore
    {
        private readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();

        private readonly RocksDb db;
        private readonly ConcurrentDictionary<byte, DataTracker> dataTrackers = new ConcurrentDictionary<byte, DataTracker>();
        private readonly ReadOptions readOptions = new ReadOptions();
        
        public CheckpointStorage(string path)
        {
            var columnFamilies = RocksDbStorage.GetColumnFamilies(path);
            db = RocksDb.OpenReadOnly(new DbOptions(), path, columnFamilies, false);

            foreach (var kvp in RocksDbStorage.GetColumnFamilyCache(db, columnFamilies))
            {
                if (!dataTrackers.TryAdd(kvp.Key, new DataTracker(this, kvp.Value)))
                {
                    // TODO: exception message
                    throw new Exception();
                }
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public ISnapshot GetSnapshot() => new Snapshot(dataTrackers);

        DataTracker GetDataTracker(byte table) 
            => dataTrackers.GetOrAdd(table, _ => new DataTracker(this, null));

        public byte[]? TryGet(byte table, byte[]? key)
            => GetDataTracker(table).TryGet(key);

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
            => GetDataTracker(table).Find(prefix);

        public void Put(byte table, byte[]? key, byte[] value) 
            => GetDataTracker(table).Update(key, value);

        public void Delete(byte table, byte[]? key)
            => GetDataTracker(table).Update(key, NONE_INSTANCE);
    }
}
