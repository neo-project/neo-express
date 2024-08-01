// Copyright (C) 2015-2024 The Neo Project.
//
// PersistentTrackingStore.Snapshot.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class PersistentTrackingStore
    {
        class Snapshot : ISnapshot
        {
            readonly RocksDb db;
            readonly ColumnFamilyHandle columnFamily;
            readonly IReadOnlyStore store;
            readonly RocksDbSharp.Snapshot snapshot;
            readonly ReadOptions readOptions;
            readonly WriteBatch writeBatch;

            public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily, IReadOnlyStore store)
            {
                this.db = db;
                this.columnFamily = columnFamily;
                this.store = store;

                snapshot = db.CreateSnapshot();
                readOptions = new ReadOptions()
                    .SetSnapshot(snapshot)
                    .SetFillCache(false);
                writeBatch = new WriteBatch();
            }

            public void Dispose()
            {
                snapshot.Dispose();
                writeBatch.Dispose();
            }

            public byte[]? TryGet(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.TryGet(key, db, columnFamily, readOptions, store);
            }

            public bool Contains(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.Contains(key, db, columnFamily, readOptions, store);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return PersistentTrackingStore.Seek(key, direction, db, columnFamily, readOptions, store);
            }

            public unsafe void Put(byte[]? key, byte[] value)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                if (value is null)
                    throw new NullReferenceException(nameof(value));

                key ??= Array.Empty<byte>();
                writeBatch.PutVector(columnFamily, key, UPDATED_PREFIX, value);
            }

            public void Delete(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                key ??= Array.Empty<byte>();
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
