// Copyright (C) 2015-2024 The Neo Project.
//
// NullStore.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullStore : IReadOnlyStore
    {
        public static readonly NullStore Instance = new NullStore();

        NullStore() { }

        public bool Contains(byte[]? key) => false;
        public byte[]? TryGet(byte[]? key) => null;
        public IEnumerable<(byte[] Key, byte[]? Value)> Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[]?)>();
    }
}
