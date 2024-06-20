// Copyright (C) 2015-2024 The Neo Project.
//
// StateServiceStore.RocksDbCacheClient.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public partial class StateServiceStore
    {
        internal sealed class RocksDbCacheClient : ICacheClient
        {
            readonly RocksDb db;
            readonly bool shared;
            readonly string familyNamePrefix;
            bool disposed;

            public RocksDbCacheClient(RocksDb db, bool shared, string familyNamePrefix)
            {
                this.db = db;
                this.shared = shared;
                this.familyNamePrefix = familyNamePrefix;
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    if (!shared)
                        db.Dispose();
                    disposed = true;
                }
            }

            // Note, CachedFoundState uses a period delinator while CachedStorage uses a dash.
            //       This is purposeful to avoid column name collisions

            string GetCachedFoundStateFamilyName(UInt160 contractHash, byte? prefix)
            {
                return prefix.HasValue
                    ? $"{familyNamePrefix}.{contractHash}.{prefix.Value}"
                    : $"{familyNamePrefix}.{contractHash}";
            }

            string GetCachedStorageFamilyName(UInt160 contractHash) => $"{familyNamePrefix}-{contractHash}";

            const byte NULL_PREFIX = 0;
            readonly static ReadOnlyMemory<byte> nullPrefix = (new byte[] { NULL_PREFIX }).AsMemory();
            readonly static ReadOnlyMemory<byte> notNullPrefix = (new byte[] { NULL_PREFIX + 1 }).AsMemory();

            public bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetCachedStorageFamilyName(contractHash);
                if (db.TryGetColumnFamily(familyName, out var columnFamily))
                {
                    using var slice = db.GetSlice(key.Span, columnFamily);
                    if (slice.Valid)
                    {
                        var span = slice.GetValue();
                        value = span.Length != 1 || span[0] != NULL_PREFIX
                            ? span[1..].ToArray()
                            : null;
                        return true;
                    }
                }

                value = null;
                return false;
            }

            public void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetCachedStorageFamilyName(contractHash);
                var columnFamily = RocksDbUtility.GetOrCreateColumnFamily(db, familyName);

                if (value is null)
                {
                    db.Put(key.Span, nullPrefix.Span, columnFamily);
                }
                else
                {
                    using var batch = new WriteBatch();
                    batch.PutVector(columnFamily, key, notNullPrefix, value.AsMemory());
                    db.Write(batch);
                }
            }

            public bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetCachedFoundStateFamilyName(contractHash, prefix);
                if (db.TryGetColumnFamily(familyName, out var columnFamily))
                {
                    value = GetCachedFoundStates(columnFamily);
                    return true;
                }

                value = Enumerable.Empty<(ReadOnlyMemory<byte>, byte[])>();
                return false;

                IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> GetCachedFoundStates(ColumnFamilyHandle columnFamily)
                {
                    using var iterator = db.NewIterator(columnFamily);
                    iterator.Seek(default(ReadOnlySpan<byte>));
                    while (iterator.Valid())
                    {
                        yield return (iterator.Key(), iterator.Value());
                        iterator.Next();
                    }
                }
            }

            public ICacheSnapshot GetFoundStatesSnapshot(UInt160 contractHash, byte? prefix)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(RocksDbCacheClient));
                var familyName = GetCachedFoundStateFamilyName(contractHash, prefix);
                var columnFamily = RocksDbUtility.GetOrCreateColumnFamily(db, familyName);
                return new Snapshot(db, columnFamily);
            }

            public void DropCachedFoundStates(UInt160 contractHash, byte? prefix)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(RocksDbCacheClient));

                var familyName = GetCachedFoundStateFamilyName(contractHash, prefix);
                db.DropColumnFamily(familyName);
            }


            class Snapshot : ICacheSnapshot
            {
                readonly RocksDb db;
                readonly ColumnFamilyHandle columnFamily;
                readonly WriteBatch writeBatch = new();

                public Snapshot(RocksDb db, ColumnFamilyHandle columnFamily)
                {
                    this.db = db;
                    this.columnFamily = columnFamily;
                }

                public void Dispose()
                {
                    writeBatch.Dispose();
                }

                public void Add(ReadOnlyMemory<byte> key, byte[] value)
                {
                    writeBatch.Put(key.Span, value.AsSpan(), columnFamily);
                }

                public void Commit()
                {
                    db.Write(writeBatch);
                }
            }
        }
    }
}
