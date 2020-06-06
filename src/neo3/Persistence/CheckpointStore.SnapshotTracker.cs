using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using OneOf;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    using TrackingDictionary = ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>>;

    partial class CheckpointStore
    {
        class SnapshotTracker
        {
            private readonly DataTracker dataTracker;
            private readonly TrackingDictionary updatedValues;
            private readonly ConcurrentDictionary<byte[], OneOf<byte[], OneOf.Types.None>> writeBatch 
                = new ConcurrentDictionary<byte[], OneOf<byte[], OneOf.Types.None>>(new ByteArrayComparer());

            public SnapshotTracker(DataTracker dataTracker, TrackingDictionary updatedValues)
            {
                this.dataTracker = dataTracker;
                this.updatedValues = updatedValues;
            }

            public byte[]? TryGet(byte[]? key)
                => dataTracker.TryGet(key, updatedValues);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[] prefix)
                => dataTracker.Find(prefix, updatedValues);

            public void Update(byte[]? key, OneOf<byte[], OneOf.Types.None> value)
            {
                writeBatch[key ?? Array.Empty<byte>()] = value;
            }

            public void Commit()
            {
                foreach (var kvp in writeBatch)
                {
                    dataTracker.Update(kvp.Key, kvp.Value);
                }
            }
        }
    }
}
