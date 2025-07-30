// Copyright (C) 2015-2025 The Neo Project.
//
// NullStore.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public class NullStore : IReadOnlyStore<byte[], byte[]>
    {
        public static readonly NullStore Instance = new NullStore();

        NullStore() { }

        public bool Contains(byte[]? key) => false;

        [Obsolete("use TryGet(byte[] key, out byte[]? value) instead.")]
        public byte[]? TryGet(byte[]? key) => null;

        public bool TryGet(byte[]? key, out byte[]? value)
        {
            value = null;
            return false;
        }

        [Obsolete("use Find(byte[]? key_prefix, SeekDirection direction) instead.")]
        public IEnumerable<(byte[] Key, byte[]? Value)> Seek(byte[] key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[]?)>();

        public IEnumerable<(byte[] Key, byte[]? Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            => Enumerable.Empty<(byte[], byte[]?)>();
    }
}
