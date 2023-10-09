// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Persistence;
using System.Collections.Generic;
using System.Linq;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullStore : IReadOnlyStore
    {
        public static readonly NullStore Instance = new NullStore();

        NullStore() { }

        public bool Contains(byte[] key) => false;
        public byte[] TryGet(byte[] key) => null;
        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[])>();
    }
}
