// Copyright (C) 2015-2026 The Neo Project.
//
// StateServiceStore.RocksDbCacheClient.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
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
            readonly DbSharedState state;
            readonly Action<WriteBatch> write;
            bool disposed;

            // Lock state must be shared by every client (and store) using the same RocksDb instance.
            sealed class DbSharedState
            {
                public readonly KeyedAsyncLock<string> DownloadLocks = new();
                public readonly object SchemaLock = new();
                public readonly ReaderWriterLockSlim Lifetime = new(LockRecursionPolicy.SupportsRecursion);
                public bool Closed;
            }

            static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RocksDb, DbSharedState> sharedStates = new();

            public RocksDbCacheClient(RocksDb db, bool shared, string familyNamePrefix, Action<WriteBatch>? write = null)
            {
                this.db = db;
                this.write = write ?? (batch => db.Write(batch));
                this.shared = shared;
                this.familyNamePrefix = familyNamePrefix;
                state = sharedStates.GetValue(db, _ => new DbSharedState());
            }

            public IDisposable AcquireDownloadLock(UInt160 contractHash, byte? prefix, CancellationToken token)
                => state.DownloadLocks.Acquire(GetCachedFoundStateFamilyName(contractHash, prefix), token);

            public Task<IDisposable> AcquireDownloadLockAsync(UInt160 contractHash, byte? prefix, CancellationToken token)
                => state.DownloadLocks.AcquireAsync(GetCachedFoundStateFamilyName(contractHash, prefix), token);

            public object DownloadLockScope => (state.DownloadLocks, familyNamePrefix);

            readonly struct DatabaseLease : IDisposable
            {
                readonly ReaderWriterLockSlim lifetime;
                public DatabaseLease(ReaderWriterLockSlim lifetime) => this.lifetime = lifetime;
                public void Dispose() => lifetime.ExitReadLock();
            }

            DatabaseLease EnterDatabase()
            {
                state.Lifetime.EnterReadLock();
                if (disposed || state.Closed)
                {
                    state.Lifetime.ExitReadLock();
                    throw new ObjectDisposedException(nameof(RocksDbCacheClient));
                }
                return new DatabaseLease(state.Lifetime);
            }

            bool TryGetColumnFamilySafe(string familyName, out ColumnFamilyHandle columnFamily)
            {
                lock (state.SchemaLock)
                    return db.TryGetColumnFamily(familyName, out columnFamily);
            }

            ColumnFamilyHandle GetOrCreateColumnFamilySafe(string familyName)
            {
                lock (state.SchemaLock)
                    return RocksDbUtility.GetOrCreateColumnFamily(db, familyName);
            }

            public void Dispose()
            {
                state.Lifetime.EnterWriteLock();
                try
                {
                    if (disposed)
                        return;
                    disposed = true;
                    if (!shared && !state.Closed)
                    {
                        state.Closed = true;
                        db.Dispose();
                    }
                }
                finally
                {
                    state.Lifetime.ExitWriteLock();
                }
            }

            // Note, CachedFoundState uses a period delinator while CachedStorage uses a dash.
            //       This is purposeful to avoid column name collisions

            string GetCachedFoundStateFamilyName(UInt160 contractHash, byte? prefix)
            {
                return prefix.HasValue
                    ? $"{familyNamePrefix}.v2.{contractHash}.{prefix.Value}"
                    : $"{familyNamePrefix}.v2.{contractHash}";
            }

            string GetCachedStorageFamilyName(UInt160 contractHash) => $"{familyNamePrefix}-{contractHash}";

            const byte NULL_PREFIX = 0;
            readonly static ReadOnlyMemory<byte> nullPrefix = (new byte[] { NULL_PREFIX }).AsMemory();
            readonly static ReadOnlyMemory<byte> notNullPrefix = (new byte[] { NULL_PREFIX + 1 }).AsMemory();
            // Version 2 reserves key 0 for completion and prefixes every storage key with 1.
            // Old families are deliberately ignored: their existence cannot prove completion.
            static readonly byte[] completionKey = new byte[] { 0 };
            static readonly byte[] completionValue = new byte[] { 1 };
            static readonly byte[] dataPrefix = new byte[] { 1 };

            public bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value)
            {
                using var lease = EnterDatabase();

                var familyName = GetCachedStorageFamilyName(contractHash);
                if (TryGetColumnFamilySafe(familyName, out var columnFamily))
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
                using var lease = EnterDatabase();

                var familyName = GetCachedStorageFamilyName(contractHash);
                var columnFamily = GetOrCreateColumnFamilySafe(familyName);

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

            public bool IsFoundStatesCached(UInt160 contractHash, byte? prefix)
            {
                using var lease = EnterDatabase();
                return TryGetCompletedFamily(contractHash, prefix, out _);
            }

            bool TryGetCompletedFamily(UInt160 contractHash, byte? prefix, out ColumnFamilyHandle columnFamily)
            {
                if (!TryGetColumnFamilySafe(GetCachedFoundStateFamilyName(contractHash, prefix), out columnFamily))
                    return false;
                using var completion = db.GetSlice(completionKey, columnFamily);
                return completion.Valid && completion.GetValue().SequenceEqual(completionValue);
            }

            public bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value)
            {
                using var lease = EnterDatabase();

                if (TryGetCompletedFamily(contractHash, prefix, out var columnFamily))
                {
                    // Do not let a native iterator escape its lifetime lease. Callers may
                    // enumerate the returned results after the store has been disposed.
                    var entries = new List<(ReadOnlyMemory<byte>, byte[])>();
                    using var iterator = db.NewIterator(columnFamily);
                    iterator.Seek(dataPrefix);
                    while (iterator.Valid())
                    {
                        var key = iterator.Key();
                        entries.Add((key.AsMemory(1), iterator.Value()));
                        iterator.Next();
                    }
                    value = entries;
                    return true;
                }

                value = Enumerable.Empty<(ReadOnlyMemory<byte>, byte[])>();
                return false;
            }

            public ICacheSnapshot GetFoundStatesSnapshot(UInt160 contractHash, byte? prefix)
            {
                using var lease = EnterDatabase();
                var familyName = GetCachedFoundStateFamilyName(contractHash, prefix);
                return new Snapshot(this, GetOrCreateColumnFamilySafe(familyName));
            }

            class Snapshot : ICacheSnapshot
            {
                readonly RocksDbCacheClient owner;
                readonly ColumnFamilyHandle columnFamily;
                readonly WriteBatch batch = new();
                readonly object gate = new();
                bool disposed;
                bool committed;

                public Snapshot(RocksDbCacheClient owner, ColumnFamilyHandle columnFamily)
                {
                    this.owner = owner;
                    this.columnFamily = columnFamily;
                }

                public void Dispose()
                {
                    lock (gate)
                    {
                        if (!disposed)
                        {
                            disposed = true;
                            batch.Dispose();
                        }
                    }
                }

                public void Add(ReadOnlyMemory<byte> key, byte[] value)
                {
                    lock (gate)
                    {
                        CheckWritable();
                        using var lease = owner.EnterDatabase();
                        var encodedKey = new byte[key.Length + 1];
                        encodedKey[0] = dataPrefix[0];
                        key.CopyTo(encodedKey.AsMemory(1));
                        batch.Put(encodedKey, value, columnFamily);
                    }
                }

                public void Commit()
                {
                    lock (gate)
                    {
                        CheckWritable();
                        using var lease = owner.EnterDatabase();
                        batch.Put(completionKey, completionValue, columnFamily);
                        owner.write(batch);
                        committed = true;
                    }
                }

                void CheckWritable()
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    if (committed)
                        throw new InvalidOperationException("Snapshot has already been committed");
                }
            }
        }
    }
}
