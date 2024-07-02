// Copyright (C) 2015-2024 The Neo Project.
//
// CheckpointStore.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public sealed class CheckpointStore : ICheckpointStore, IDisposable
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

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction) => store.Seek(key, direction);
        public byte[]? TryGet(byte[] key) => store.TryGet(key);
        public bool Contains(byte[] key) => store.Contains(key);
    }
}
