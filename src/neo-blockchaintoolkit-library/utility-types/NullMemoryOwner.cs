// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System;
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
