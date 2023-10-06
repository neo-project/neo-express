// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System;
using System.Buffers;

namespace Neo.BlockchainToolkit.Utilities
{
    class ExactMemoryOwner<T> : IMemoryOwner<T>
    {
        readonly IMemoryOwner<T> owner;
        readonly int size;

        public ExactMemoryOwner(IMemoryOwner<T> owner, int size)
        {
            this.owner = owner;
            this.size = size;
        }

        public static ExactMemoryOwner<T> Rent(int size, MemoryPool<T> pool = null)
        {
            pool ??= MemoryPool<T>.Shared;
            var owner = pool.Rent(size);
            return new ExactMemoryOwner<T>(owner, size);
        }

        public Memory<T> Memory => owner.Memory[..size];

        public void Dispose()
        {
            owner.Dispose();
        }
    }
}
