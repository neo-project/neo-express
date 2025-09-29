// Copyright (C) 2015-2025 The Neo Project.
//
// PersistentTrackingStore.Snapshot.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Utilities;
using Neo.Persistence;
using OneOf;
using RocksDbSharp;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using None = OneOf.Types.None;

namespace Neo.BlockchainToolkit.Persistence
{
    using TrackingMap = ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, None>>;

    public partial class PersistentTrackingStore
    {
        class Snapshot : IStoreSnapshot
        {
            readonly RocksDb db;
            readonly ColumnFamilyHandle columnFamily;
            readonly IReadOnlyStore<byte[], byte[]> store;
            readonly RocksDbSharp.Snapshot snapshot;
            readonly ReadOptions readOptions;
            readonly WriteBatch writeBatch;
            readonly IStore storeRef;
            TrackingMap uncommittedChanges = TrackingMap.Empty.WithComparers(MemorySequenceComparer.Default);

            public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily, IReadOnlyStore<byte[], byte[]> store, IStore storeRef)
            {
                this.db = db;
                this.columnFamily = columnFamily;
                this.store = store;
                this.storeRef = storeRef;

                snapshot = db.CreateSnapshot();
                readOptions = new ReadOptions()
                    .SetSnapshot(snapshot)
                    .SetFillCache(false);
                writeBatch = new WriteBatch();
            }

            public IStore Store => storeRef;

            public void Dispose()
            {
                snapshot.Dispose();
                writeBatch.Dispose();
            }

            [Obsolete("use TryGet(byte[] key, out byte[]? value) instead.")]
            public byte[]? TryGet(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.TryGet(key, db, columnFamily, readOptions, store);
            }

            public bool TryGet(byte[]? key, [NotNullWhen(true)] out byte[]? value)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));

                // First check if the key is in uncommitted changes
                key ??= Array.Empty<byte>();
                if (uncommittedChanges.TryGetValue(key, out var changeValue))
                {
                    if (changeValue.TryPickT0(out var changeValueData, out var _))
                    {
                        value = changeValueData.ToArray();
                        return true;
                    }
                    else
                    {
                        // Key was deleted in uncommitted changes
                        value = null;
                        return false;
                    }
                }

                // If not in uncommitted changes, check the snapshot and store
                value = PersistentTrackingStore.TryGet(key, db, columnFamily, readOptions, store);
                return value != null;
            }

            public bool Contains(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));

                // First check if the key is in uncommitted changes
                key ??= Array.Empty<byte>();
                if (uncommittedChanges.TryGetValue(key, out var changeValue))
                {
                    return changeValue.IsT0; // True if it's a value, false if it's a deletion
                }

                // If not in uncommitted changes, check the snapshot and store
                return PersistentTrackingStore.Contains(key, db, columnFamily, readOptions, store);
            }

            [Obsolete("use Find(byte[]? key_prefix, SeekDirection direction) instead.")]
            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.Seek(key, direction, db, columnFamily, readOptions, store);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.Seek(key_prefix, direction, db, columnFamily, readOptions, store);
            }

            public unsafe void Put(byte[]? key, byte[] value)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                if (value is null)
                    throw new NullReferenceException(nameof(value));

                key ??= Array.Empty<byte>();

                // Track the change in uncommitted changes
                uncommittedChanges = uncommittedChanges.SetItem(key, (ReadOnlyMemory<byte>)value);

                writeBatch.PutVector(columnFamily, key, UPDATED_PREFIX, value);
            }

            public void Delete(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                key ??= Array.Empty<byte>();

                // Track the deletion in uncommitted changes
                uncommittedChanges = uncommittedChanges.SetItem(key, default(None));

                if (store.Contains(key))
                {
                    writeBatch.Put(key.AsSpan(), DELETED_PREFIX.Span, columnFamily);
                }
                else
                {
                    writeBatch.Delete(key, columnFamily);
                }
            }

            public void Commit()
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                db.Write(writeBatch);
            }
        }
    }
}
