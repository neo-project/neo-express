// Copyright (C) 2015-2024 The Neo Project.
//
// MemoryTrackingStore.Snapshot.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Utilities;
using Neo.Persistence;
using OneOf;
using System.Collections.Immutable;
using None = OneOf.Types.None;
namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    public partial class MemoryTrackingStore
    {
        class Snapshot : IStoreSnapshot
        {
            readonly IReadOnlyStore<byte[], byte[]> store;
            readonly TrackingMap trackingMap;
            readonly Action<TrackingMap> commitAction;
            readonly IStore storeRef;
            TrackingMap writeBatchMap = TrackingMap.Empty.WithComparers(MemorySequenceComparer.Default);

            public Snapshot(IReadOnlyStore<byte[], byte[]> store, TrackingMap trackingMap, Action<TrackingMap> commitAction, IStore storeRef)
            {
                this.store = store;
                this.trackingMap = trackingMap;
                this.commitAction = commitAction;
                this.storeRef = storeRef;
            }

            public IStore Store => storeRef;

            public void Dispose() { }

            [Obsolete("use TryGet(byte[] key, out byte[]? value) instead.")]
            public byte[]? TryGet(byte[]? key) => MemoryTrackingStore.TryGet(key, trackingMap, store);

            public bool TryGet(byte[]? key, out byte[]? value)
            {
                // First check if the key is in the write batch
                key ??= Array.Empty<byte>();
                if (writeBatchMap.TryGetValue(key, out var batchValue))
                {
                    if (batchValue.TryPickT0(out var batchValueData, out var _))
                    {
                        value = batchValueData.ToArray();
                        return true;
                    }
                    else
                    {
                        // Key was deleted in write batch
                        value = null;
                        return false;
                    }
                }

                // If not in write batch, check the original tracking map and store
                value = MemoryTrackingStore.TryGet(key, trackingMap, store);
                return value != null;
            }

            public bool Contains(byte[]? key)
            {
                // First check if the key is in the write batch
                key ??= Array.Empty<byte>();
                if (writeBatchMap.TryGetValue(key, out var batchValue))
                {
                    return batchValue.IsT0; // True if it's a value, false if it's a deletion
                }

                // If not in write batch, check the original tracking map and store
                return MemoryTrackingStore.Contains(key, trackingMap, store);
            }

            [Obsolete("use Find(byte[]? key_prefix, SeekDirection direction) instead.")]
            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
                => MemoryTrackingStore.Seek(key, direction, trackingMap, store);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
                => MemoryTrackingStore.Seek(key_prefix, direction, trackingMap, store);

            public void Put(byte[]? key, byte[]? value)
            {
                if (value is null)
                    throw new NullReferenceException(nameof(value));
                MemoryTrackingStore.AtomicUpdate(ref writeBatchMap, key, (ReadOnlyMemory<byte>)value);
            }

            public void Delete(byte[]? key)
            {
                MemoryTrackingStore.AtomicUpdate(ref writeBatchMap, key, default(None));
            }

            public void Commit() => commitAction(writeBatchMap);
        }
    }
}
