// Copyright (C) 2015-2026 The Neo Project.
//
// StateServiceStore.MemoryCacheClient.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Utilities;
using System.Collections.Concurrent;

namespace Neo.BlockchainToolkit.Persistence
{
    public sealed partial class StateServiceStore
    {
        internal sealed class MemoryCacheClient : ICacheClient
        {
            // Key the caches by the actual (contract, key) pair rather than by its 32-bit
            // hash code. Using the hash as the identity meant two distinct storage keys
            // could collide and silently serve each other's cached value.
            readonly ConcurrentDictionary<(UInt160 contractHash, ReadOnlyMemory<byte> key), byte[]?> storageMap = new(StorageKeyComparer.Instance);
            readonly ConcurrentDictionary<(UInt160 contractHash, byte? prefix), IList<(ReadOnlyMemory<byte>, byte[])>> foundStateMap = new();
            private bool disposed;

            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                }
            }

            sealed class StorageKeyComparer : IEqualityComparer<(UInt160 contractHash, ReadOnlyMemory<byte> key)>
            {
                public static StorageKeyComparer Instance { get; } = new();

                public bool Equals((UInt160 contractHash, ReadOnlyMemory<byte> key) x, (UInt160 contractHash, ReadOnlyMemory<byte> key) y)
                    => x.contractHash == y.contractHash
                        && MemorySequenceComparer.Equals(x.key.Span, y.key.Span);

                public int GetHashCode((UInt160 contractHash, ReadOnlyMemory<byte> key) obj)
                {
                    var hashBuilder = new HashCode();
                    hashBuilder.Add(obj.contractHash);
                    hashBuilder.AddBytes(obj.key.Span);
                    return hashBuilder.ToHashCode();
                }
            }

            public bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                return storageMap.TryGetValue((contractHash, key), out value);
            }

            public void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                // Copy the key so the stored identity stays stable even if the caller
                // reuses the underlying buffer.
                if (!storageMap.TryAdd((contractHash, key.ToArray()), value))
                    throw new Exception($"Key already exists {Convert.ToHexString(key.Span)}");
            }

            public bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                if (foundStateMap.TryGetValue((contractHash, prefix), out var list))
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

                if (foundStateMap.ContainsKey((contractHash, prefix)))
                    throw new Exception($"{contractHash}-{prefix} already cached");
                return new Snapshot(foundStateMap, (contractHash, prefix));
            }

            public void DropCachedFoundStates(UInt160 contractHash, byte? prefix)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MemoryCacheClient));

                _ = foundStateMap.TryRemove((contractHash, prefix), out _);
            }

            class Snapshot : ICacheSnapshot
            {
                readonly ConcurrentDictionary<(UInt160 contractHash, byte? prefix), IList<(ReadOnlyMemory<byte>, byte[])>> foundStateMap;
                readonly (UInt160 contractHash, byte? prefix) key;
                List<(ReadOnlyMemory<byte> key, byte[] value)> entries = new();
                bool disposed = false;

                public Snapshot(ConcurrentDictionary<(UInt160 contractHash, byte? prefix), IList<(ReadOnlyMemory<byte>, byte[])>> foundStateMap, (UInt160 contractHash, byte? prefix) key)
                {
                    this.foundStateMap = foundStateMap;
                    this.key = key;
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
                    ObjectDisposedException.ThrowIf(disposed, nameof(MemoryCacheClient.Snapshot));
                    entries.Add((key, value));
                }

                public void Commit()
                {
                    ObjectDisposedException.ThrowIf(disposed, nameof(MemoryCacheClient.Snapshot));

                    if (!foundStateMap.TryAdd(key, entries))
                    {
                        throw new Exception("Failed to add cached entries");
                    }
                    // Just in case Add it's called after commit
                    entries = new();
                }
            }
        }
    }
}
