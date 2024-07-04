// Copyright (C) 2015-2024 The Neo Project.
//
// RocksDbStore.cs file belongs to neo-express project and is free
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
    public sealed partial class RocksDbStore : IStore
    {
        readonly RocksDb db;
        readonly ColumnFamilyHandle columnFamily;
        readonly bool readOnly;
        readonly bool shared;
        bool disposed;

        public RocksDbStore(RocksDb db, string? columnFamilyName = null, bool readOnly = false, bool shared = false)
            : this(db, db.GetColumnFamilyOrDefault(columnFamilyName), readOnly, shared)
        {
        }

        public RocksDbStore(RocksDb db, ColumnFamilyHandle columnFamily, bool readOnly = false, bool shared = false)
        {
            this.db = db;
            this.columnFamily = columnFamily;
            this.readOnly = readOnly;
            this.shared = shared;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            if (!shared)
            {
                db.Dispose();
            }
            disposed = true;
        }

        public byte[]? TryGet(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RocksDbStore));
            return db.Get(key ?? Array.Empty<byte>(), columnFamily);
        }

        public bool Contains(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RocksDbStore));
            using var slice = db.GetSlice(key, columnFamily);
            return slice.Valid;
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
        {
            if (disposed || db.Handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RocksDbStore));
            return Seek(key, direction, db, columnFamily);
        }

        public static IEnumerable<(byte[] Key, byte[] Value)> Seek(
            byte[]? prefix, SeekDirection direction, RocksDb db,
            ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null)
        {
            prefix ??= Array.Empty<byte>();
            readOptions ??= RocksDbUtility.DefaultReadOptions;
            var forward = direction == SeekDirection.Forward;

            using var iterator = db.NewIterator(columnFamily, readOptions);

            _ = forward ? iterator.Seek(prefix) : iterator.SeekForPrev(prefix);
            while (iterator.Valid())
            {
                yield return (iterator.Key(), iterator.Value());
                _ = forward ? iterator.Next() : iterator.Prev();
            }
        }

        public void Put(byte[]? key, byte[]? value)
        {
            if (disposed || db.Handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly)
                throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily);
        }

        public void PutSync(byte[]? key, byte[]? value)
        {
            if (disposed || db.Handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly)
                throw new InvalidOperationException("read only");
            db.Put(key ?? Array.Empty<byte>(), value, columnFamily, RocksDbUtility.WriteSyncOptions);
        }

        public void Delete(byte[]? key)
        {
            if (disposed || db.Handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly)
                throw new InvalidOperationException("read only");
            db.Remove(key ?? Array.Empty<byte>(), columnFamily);
        }

        public ISnapshot GetSnapshot()
        {
            if (disposed || db.Handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RocksDbStore));
            if (readOnly)
                throw new InvalidOperationException("read only");
            return new Snapshot(db, columnFamily);
        }
    }
}
