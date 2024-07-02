// Copyright (C) 2015-2024 The Neo Project.
//
// StateServiceStore.MemoryCacheClient.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Concurrent;

namespace Neo.BlockchainToolkit.Persistence
{
    public sealed partial class StateServiceStore
    {
        internal sealed class MemoryCacheClient : ICacheClient
        {
            readonly ConcurrentDictionary<int, byte[]?> storageMap = new();
            readonly ConcurrentDictionary<int, IList<(ReadOnlyMemory<byte>, byte[])>> foundStateMap = new();
            private bool disposed;

            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                }
            }

            static int GetStorageKey(UInt160 contractHash, byte? prefix)
            {
                var hashBuilder = new HashCode();
                hashBuilder.Add(contractHash);
                if (prefix.HasValue)
                    hashBuilder.Add(prefix.Value);
                return hashBuilder.ToHashCode();
            }

            static int GetStorageKey(UInt160 contractHash, ReadOnlyMemory<byte> key)
            {
                var hashBuilder = new HashCode();
                hashBuilder.Add(contractHash);
                hashBuilder.AddBytes(key.Span);
                return hashBuilder.ToHashCode();
            }

            public bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                var hash = GetStorageKey(contractHash, key);
                return storageMap.TryGetValue(hash, out value);
            }

            public void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                var hash = GetStorageKey(contractHash, key);
                if (!storageMap.TryAdd(hash, value))
                    throw new Exception($"Key already exists {Convert.ToHexString(key.Span)}");
            }

            public bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                var hash = GetStorageKey(contractHash, prefix);
                if (foundStateMap.TryGetValue(hash, out var list))
                {
                    value = list;
                    return true;
                }

                value = Enumerable.Empty<(ReadOnlyMemory<byte> key, byte[] value)>();
                return false;
            }

            public ICacheSnapshot GetFoundStatesSnapshot(UInt160 contractHash, byte? prefix)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                var hash = GetStorageKey(contractHash, prefix);
                if (foundStateMap.ContainsKey(hash))
                    throw new Exception($"{contractHash}-{prefix} already cached");
                return new Snapshot(foundStateMap, hash);
            }

            public void DropCachedFoundStates(UInt160 contractHash, byte? prefix)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                var hash = GetStorageKey(contractHash, prefix);
                _ = foundStateMap.TryRemove(hash, out _);
            }

            class Snapshot : ICacheSnapshot
            {
                readonly ConcurrentDictionary<int, IList<(ReadOnlyMemory<byte>, byte[])>> foundStateMap;
                readonly int hash;
                readonly List<(ReadOnlyMemory<byte> key, byte[] value)> entries = new();
                bool disposed = false;

                public Snapshot(ConcurrentDictionary<int, IList<(ReadOnlyMemory<byte>, byte[])>> foundStateMap, int hash)
                {
                    this.foundStateMap = foundStateMap;
                    this.hash = hash;
                }

                public void Dispose()
                {
                    if (!disposed)
                    {
                        entries.Clear();
                        disposed = true;
                    }
                }

                public void Add(ReadOnlyMemory<byte> key, byte[] value)
                {
                    if (disposed)
                        throw new ObjectDisposedException(nameof(MemoryCacheClient.Snapshot));
                    entries.Add((key, value));
                }

                public void Commit()
                {
                    if (disposed)
                        throw new ObjectDisposedException(nameof(MemoryCacheClient.Snapshot));
                    if (!foundStateMap.TryAdd(hash, entries))
                    {
                        throw new Exception("Failed to add cached entries");
                    }
                }
            }
        }
    }
}
