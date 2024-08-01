// Copyright (C) 2015-2024 The Neo Project.
//
// MemoryTrackingStore.cs file belongs to neo-express project and is free
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

    public partial class MemoryTrackingStore : IStore
    {
        readonly IReadOnlyStore store;
        TrackingMap trackingMap = TrackingMap.Empty.WithComparers(MemorySequenceComparer.Default);

        public MemoryTrackingStore(IReadOnlyStore store)
        {
            this.store = store;
        }

        public void Dispose()
        {
            (store as IDisposable)?.Dispose();
            GC.SuppressFinalize(this);
        }

        public ISnapshot GetSnapshot() => new Snapshot(store, trackingMap, this.CommitSnapshot);

        public byte[]? TryGet(byte[]? key) => TryGet(key, trackingMap, store);

        static byte[]? TryGet(byte[]? key, TrackingMap trackingMap, IReadOnlyStore store)
        {
            key ??= Array.Empty<byte>();
            if (trackingMap.TryGetValue(key, out var mapValue))
            {
                return mapValue.TryPickT0(out var value, out var _)
                    ? value.ToArray()
                    : null;
            }

            return store.TryGet(key);
        }

        public bool Contains(byte[]? key) => Contains(key, trackingMap, store);

        static bool Contains(byte[]? key, TrackingMap trackingMap, IReadOnlyStore store)
        {
            key ??= Array.Empty<byte>();
            return trackingMap.TryGetValue(key, out var mapValue)
                ? mapValue.IsT0
                : store.Contains(key);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            => Seek(key, direction, trackingMap, store);

        static IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction, TrackingMap trackingMap, IReadOnlyStore store)
        {
            key ??= Array.Empty<byte>();
            if (key.Length == 0 && direction == SeekDirection.Backward)
            {
                return Enumerable.Empty<(byte[] key, byte[] value)>();
            }

            var comparer = direction == SeekDirection.Forward
                ? MemorySequenceComparer.Default
                : MemorySequenceComparer.Reverse;

            var memoryItems = trackingMap
                .Where(kvp => kvp.Value.IsT0)
                .Where(kvp => key.Length == 0 || comparer.Compare(kvp.Key, key) >= 0)
                .Select(kvp => (Key: kvp.Key.ToArray(), Value: kvp.Value.AsT0.ToArray()));

            var storeItems = store
                .Seek(key, direction)
                .Where<(byte[] Key, byte[] Value)>(kvp => !trackingMap.ContainsKey(kvp.Key));

            return memoryItems.Concat(storeItems).OrderBy(kvp => kvp.Key, comparer);
        }

        public void Put(byte[]? key, byte[]? value)
        {
            if (value is null)
                throw new NullReferenceException(nameof(value));
            AtomicUpdate(ref trackingMap, key, (ReadOnlyMemory<byte>)value);
        }

        public void Delete(byte[]? key)
        {
            AtomicUpdate(ref trackingMap, key, default(None));
        }

        static void AtomicUpdate(ref TrackingMap trackingMap, byte[]? key, OneOf<ReadOnlyMemory<byte>, None> value)
        {
            key = key is null ? Array.Empty<byte>() : key.AsSpan().ToArray();
            value = value.MapT0(memory => (ReadOnlyMemory<byte>)memory.ToArray().AsMemory());
            var priorCollection = Volatile.Read(ref trackingMap);
            do
            {
                var updatedCollection = priorCollection.SetItem(key, value);
                var interlockedResult = Interlocked.CompareExchange(ref trackingMap, updatedCollection, priorCollection);
                if (object.ReferenceEquals(priorCollection, interlockedResult))
                    break;
                priorCollection = interlockedResult;
            }
            while (true);
        }

        void CommitSnapshot(TrackingMap writeBatchMap)
        {
            var priorCollection = Volatile.Read(ref trackingMap);
            do
            {
                var updatedCollection = Volatile.Read(ref trackingMap);
                foreach (var change in writeBatchMap)
                {
                    updatedCollection = updatedCollection.SetItem(change.Key, change.Value);
                }

                var interlockedResult = Interlocked.CompareExchange(ref trackingMap, updatedCollection, priorCollection);
                if (object.ReferenceEquals(priorCollection, interlockedResult))
                    break;
                priorCollection = interlockedResult;
            }
            while (true);
        }
    }
}
