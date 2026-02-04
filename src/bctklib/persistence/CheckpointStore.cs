// Copyright (C) 2015-2025 The Neo Project.
//
// CheckpointStore.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using Neo.SmartContract;
using System.Diagnostics.CodeAnalysis;

namespace Neo.BlockchainToolkit.Persistence
{
    public sealed class CheckpointStore : ICheckpointStore, IReadOnlyStore<byte[], byte[]>, IDisposable
    {
        readonly RocksDbStore store;
        readonly string checkpointTempPath;

        public ProtocolSettings Settings { get; }
        internal string CheckpointTempPath => checkpointTempPath;

        public CheckpointStore(string checkpointPath, ExpressChain? chain, UInt160? scriptHash = null)
            : this(checkpointPath, chain?.Network, chain?.AddressVersion, scriptHash)
        {
        }

        public CheckpointStore(string checkpointPath, uint? network = null, byte? addressVersion = null, UInt160? scriptHash = null, string? columnFamilyName = null)
        {
            checkpointTempPath = RocksDbUtility.GetTempPath();
            var metadata = RocksDbUtility.RestoreCheckpoint(checkpointPath, checkpointTempPath, network, addressVersion, scriptHash);

            Settings = ProtocolSettings.Default with
            {
                Network = metadata.network,
                AddressVersion = metadata.addressVersion,
            };
            var db = RocksDbUtility.OpenReadOnlyDb(checkpointTempPath);
            store = new RocksDbStore(db, columnFamilyName, readOnly: true);
        }

        public void Dispose()
        {
            store.Dispose();

            if (!string.IsNullOrEmpty(checkpointTempPath)
                && Directory.Exists(checkpointTempPath))
            {
                Directory.Delete(checkpointTempPath, true);
            }
        }

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
        public byte[]? TryGet(byte[] key) => store.TryGet(key);

        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value) => store.TryGet(key, out value);

        public bool Contains(byte[] key) => store.Contains(key);

        [Obsolete("use Find(byte[]? key_prefix, SeekDirection direction) instead.")]
        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction) => store.Seek(key, direction);

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward) => store.Find(key_prefix, direction);

        // IReadOnlyStore<StorageKey, StorageItem> implementation
        public StorageItem this[StorageKey key]
        {
            get
            {
                if (TryGet(key, out var value))
                    return value;
                throw new KeyNotFoundException($"Key not found: {Convert.ToHexString(key.ToArray())}");
            }
        }

        [Obsolete("use TryGet(StorageKey key, out StorageItem? value) instead.")]
        public StorageItem? TryGet(StorageKey key)
        {
            var keyBytes = key.ToArray();
            var valueBytes = store.TryGet(keyBytes);
            return valueBytes != null ? new StorageItem(valueBytes) : null;
        }

        public bool TryGet(StorageKey key, [NotNullWhen(true)] out StorageItem? value)
        {
            var keyBytes = key.ToArray();
            if (store.TryGet(keyBytes, out var valueBytes))
            {
                value = new StorageItem(valueBytes);
                return true;
            }
            value = null;
            return false;
        }

        public bool Contains(StorageKey key)
        {
            var keyBytes = key.ToArray();
            return store.Contains(keyBytes);
        }

        public IEnumerable<(StorageKey Key, StorageItem Value)> Find(StorageKey? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
        {
            var prefixBytes = key_prefix?.ToArray();
            foreach (var (keyBytes, valueBytes) in store.Find(prefixBytes, direction))
            {
                yield return ((StorageKey)keyBytes, new StorageItem(valueBytes));
            }
        }
    }
}
