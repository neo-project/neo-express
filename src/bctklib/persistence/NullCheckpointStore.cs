// Copyright (C) 2015-2025 The Neo Project.
//
// NullCheckpointStore.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullCheckpointStore : ICheckpointStore
    {
        public ProtocolSettings Settings { get; }

        public NullCheckpointStore(ExpressChain? chain)
            : this(chain?.Network, chain?.AddressVersion)
        {
        }

        public NullCheckpointStore(uint? network = null, byte? addressVersion = null)
        {
            this.Settings = ProtocolSettings.Default with
            {
                Network = network ?? ProtocolSettings.Default.Network,
                AddressVersion = addressVersion ?? ProtocolSettings.Default.AddressVersion,
            };
        }

        public IEnumerable<(byte[] Key, byte[]? Value)> Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[]?)>();
        public byte[]? TryGet(byte[] key) => null;
        public bool Contains(byte[] key) => false;

        // IReadOnlyStore<StorageKey, StorageItem> implementation
        public StorageItem this[StorageKey key]
        {
            get => throw new KeyNotFoundException($"Key not found: {Convert.ToHexString(key.ToArray())}");
        }

        [Obsolete("use TryGet(StorageKey key, out StorageItem? value) instead.")]
        public StorageItem? TryGet(StorageKey key) => null;

        public bool TryGet(StorageKey key, out StorageItem? value)
        {
            value = null;
            return false;
        }

        public bool Contains(StorageKey key) => false;

        public IEnumerable<(StorageKey Key, StorageItem Value)> Find(StorageKey? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            => Enumerable.Empty<(StorageKey, StorageItem)>();
    }
}
