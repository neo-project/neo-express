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
        class Snapshot : ISnapshot
        {
            readonly IReadOnlyStore store;
            readonly TrackingMap trackingMap;
            readonly Action<TrackingMap> commitAction;
            TrackingMap writeBatchMap = TrackingMap.Empty.WithComparers(MemorySequenceComparer.Default);

            public Snapshot(IReadOnlyStore store, TrackingMap trackingMap, Action<TrackingMap> commitAction)
            {
                this.store = store;
                this.trackingMap = trackingMap;
                this.commitAction = commitAction;
            }

            public void Dispose() { }

            public byte[]? TryGet(byte[]? key) => MemoryTrackingStore.TryGet(key, trackingMap, store);

            public bool Contains(byte[]? key) => MemoryTrackingStore.Contains(key, trackingMap, store);

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
                => MemoryTrackingStore.Seek(key, direction, trackingMap, store);

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
