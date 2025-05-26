// Copyright (C) 2015-2024 The Neo Project.
//
// StoreSnapshotAdapter.cs file belongs to neo-express project and is free
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
    /// Adapter that bridges legacy ISnapshot implementations to Neo 3.8.2 IStoreSnapshot interface.
    /// This allows existing neo-express snapshot implementations to work with the new Neo persistence architecture.
    /// </summary>
    public class StoreSnapshotAdapter : IStoreSnapshot
    {
        private readonly ISnapshot legacySnapshot;
        private readonly IStore store;

        public StoreSnapshotAdapter(ISnapshot legacySnapshot, IStore store)
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
                throw new KeyNotFoundException();
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
    /// Reverse adapter that allows Neo 3.8.2 IStoreSnapshot to be used as legacy ISnapshot.
    /// This enables gradual migration from legacy interfaces.
    /// </summary>
    public class LegacySnapshotAdapter : ISnapshot
    {
        private readonly IStoreSnapshot storeSnapshot;

        public LegacySnapshotAdapter(IStoreSnapshot storeSnapshot)
        {
            this.storeSnapshot = storeSnapshot ?? throw new ArgumentNullException(nameof(storeSnapshot));
        }

        public void Dispose() => storeSnapshot.Dispose();

        [Obsolete("use TryGet(byte[] key, out byte[]? value) instead.")]
        public byte[]? TryGet(byte[]? key) => storeSnapshot.TryGet(key ?? Array.Empty<byte>());

        public bool TryGet(byte[]? key, out byte[]? value)
        {
            return storeSnapshot.TryGet(key ?? Array.Empty<byte>(), out value);
        }

        public bool Contains(byte[]? key) => storeSnapshot.Contains(key ?? Array.Empty<byte>());

        [Obsolete("use Find(byte[]? key_prefix, SeekDirection direction) instead.")]
        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction)
        {
            return storeSnapshot.Find(key, direction);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
        {
            return storeSnapshot.Find(key_prefix, direction);
        }

        public void Put(byte[]? key, byte[]? value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            storeSnapshot.Put(key ?? Array.Empty<byte>(), value);
        }

        public void Delete(byte[]? key) => storeSnapshot.Delete(key ?? Array.Empty<byte>());

        public void Commit() => storeSnapshot.Commit();
    }
}
