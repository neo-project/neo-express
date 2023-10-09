// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class RocksDbStore
    {
        class Snapshot : ISnapshot
        {
            readonly RocksDb db;
            readonly ColumnFamilyHandle columnFamily;
            readonly RocksDbSharp.Snapshot snapshot;
            readonly ReadOptions readOptions;
            readonly WriteBatch writeBatch;

            public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily)
            {
                this.db = db;
                this.columnFamily = columnFamily;
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

            public byte[] TryGet(byte[] key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return db.Get(key ?? Array.Empty<byte>(), columnFamily, readOptions);
            }

            public bool Contains(byte[] key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                using var slice = db.GetSlice(key, columnFamily, readOptions);
                return slice.Valid;
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                return RocksDbStore.Seek(key, direction, db, columnFamily, readOptions);
            }

            public void Put(byte[] key, byte[] value)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                writeBatch.Put(key ?? Array.Empty<byte>(), value, columnFamily);
            }

            public void Delete(byte[] key)
            {
                if (snapshot.Handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(Snapshot));
                writeBatch.Delete(key ?? Array.Empty<byte>(), columnFamily);
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
