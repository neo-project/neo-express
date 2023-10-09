// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using System.Collections.Generic;
using System.Linq;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullCheckpointStore : ICheckpointStore
    {
        public ProtocolSettings Settings { get; }

        public NullCheckpointStore(ExpressChain chain)
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

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[])>();
        public byte[] TryGet(byte[] key) => null;
        public bool Contains(byte[] key) => false;
    }
}
