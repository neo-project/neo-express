// Copyright (C) 2015-2024 The Neo Project.
//
// Neo382StoreSnapshot.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using System.Diagnostics.CodeAnalysis;

namespace Neo.BlockchainToolkit.Persistence
{
    /// <summary>
    /// Neo 3.8.2 compatible IStoreSnapshot implementation that wraps legacy ISnapshot implementations.
    /// This allows existing neo-express stores to work with the new Neo persistence architecture.
    /// </summary>
    public class Neo382StoreSnapshot : IStoreSnapshot
    {
        private readonly ISnapshot legacySnapshot;
        private readonly IStore store;

        public Neo382StoreSnapshot(ISnapshot legacySnapshot, IStore store)
        {
            this.legacySnapshot = legacySnapshot ?? throw new ArgumentNullException(nameof(legacySnapshot));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public IStore Store => store;

        public void Commit() => legacySnapshot.Commit();

        public void Dispose() => legacySnapshot.Dispose();

        // IReadOnlyStore<byte[], byte[]> implementation
        public byte[] this[byte[] key] 
        { 
            get 
            {
                if (TryGet(key, out var value))
                    return value;
                throw new KeyNotFoundException($"Key not found: {Convert.ToHexString(key)}");
            }
        }

        [Obsolete("use TryGet(byte[] key, out byte[]? value) instead.")]
        public byte[]? TryGet(byte[] key) => legacySnapshot.TryGet(key);

        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
        {
            return legacySnapshot.TryGet(key, out value);
        }

        public bool Contains(byte[] key) => legacySnapshot.Contains(key);

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
        {
            return legacySnapshot.Find(key_prefix, direction);
        }

        // IWriteStore<byte[], byte[]> implementation
        public void Delete(byte[] key) => legacySnapshot.Delete(key);

        public void Put(byte[] key, byte[] value) => legacySnapshot.Put(key, value);

        public void PutSync(byte[] key, byte[] value) => Put(key, value);
    }

    /// <summary>
    /// Extension methods to help with Neo 3.8.2 compatibility.
    /// </summary>
    public static class Neo382StoreExtensions
    {
        /// <summary>
        /// Creates a Neo 3.8.2 compatible IStoreSnapshot from a legacy ISnapshot.
        /// </summary>
        /// <param name="store">The store that created the snapshot.</param>
        /// <param name="legacySnapshot">The legacy snapshot to wrap.</param>
        /// <returns>A Neo 3.8.2 compatible IStoreSnapshot.</returns>
        public static IStoreSnapshot ToNeo382Snapshot(this IStore store, ISnapshot legacySnapshot)
        {
            return new Neo382StoreSnapshot(legacySnapshot, store);
        }

        /// <summary>
        /// Creates a legacy ISnapshot from a Neo 3.8.2 IStoreSnapshot.
        /// </summary>
        /// <param name="storeSnapshot">The Neo 3.8.2 snapshot to wrap.</param>
        /// <returns>A legacy ISnapshot.</returns>
        public static ISnapshot ToLegacySnapshot(this IStoreSnapshot storeSnapshot)
        {
            return new LegacySnapshotAdapter(storeSnapshot);
        }
    }
}
