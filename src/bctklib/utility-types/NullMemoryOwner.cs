// Copyright (C) 2015-2024 The Neo Project.
//
// NullMemoryOwner.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Buffers;

namespace Neo.BlockchainToolkit.Utilities
{
    class NullMemoryOwner<T> : IMemoryOwner<T>
    {
        public static readonly NullMemoryOwner<T> Instance = new();

        public Memory<T> Memory => default;

        public void Dispose() { }
    }
}
