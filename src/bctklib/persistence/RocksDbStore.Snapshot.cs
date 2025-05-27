// Copyright (C) 2015-2024 The Neo Project.
//
// RocksDbStore.Snapshot.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Utilities;
using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStore
    {
        class Snapshot : IStoreSnapshot
        {
            readonly RocksDb db;
            readonly ColumnFamilyHandle columnFamily;
            readonly RocksDbSharp.Snapshot snapshot;
            readonly ReadOptions readOptions;
            readonly IStore store;
            readonly WriteBatch writeBatch;
            readonly Dictionary<byte[], byte[]?> pendingWrites = new(new ByteArrayEqualityComparer());

            public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily, IStore store)
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

            public IStore Store => store;

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
                return db.Get(key ?? Array.Empty<byte>(), columnFamily, readOptions);
            }

            public bool TryGet(byte[]? key, out byte[]? value)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));

                var keyBytes = key ?? Array.Empty<byte>();

                // First check pending writes
                if (pendingWrites.TryGetValue(keyBytes, out value))
                {
                    return value != null; // null means deleted
                }

                // Then check the database snapshot
                value = db.Get(keyBytes, columnFamily, readOptions);
                return value != null;
            }

            public bool Contains(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));

                return TryGet(key, out _);
            }

            [Obsolete("use Find(byte[]? key_prefix, SeekDirection direction) instead.")]
            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return RocksDbStore.Seek(key, direction, db, columnFamily, readOptions);
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return RocksDbStore.Seek(key_prefix, direction, db, columnFamily, readOptions);
            }

            public void Put(byte[]? key, byte[] value)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                var keyBytes = key ?? Array.Empty<byte>();
                writeBatch.Put(keyBytes, value, columnFamily);
                pendingWrites[keyBytes] = value;
            }

            public void Delete(byte[]? key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                var keyBytes = key ?? Array.Empty<byte>();
                writeBatch.Delete(keyBytes, columnFamily);
                pendingWrites[keyBytes] = null; // null indicates deletion
            }

            public void Commit()
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                db.Write(writeBatch);
                pendingWrites.Clear();
            }
        }

        class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x == null && y == null)
                    return true;
                if (x == null || y == null)
                    return false;
                return x.AsSpan().SequenceEqual(y.AsSpan());
            }

            public int GetHashCode(byte[] obj)
            {
                if (obj == null)
                    return 0;
                HashCode hash = default;
                hash.AddBytes(obj);
                return hash.ToHashCode();
            }
        }
    }
}
