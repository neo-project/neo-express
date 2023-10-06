// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Utilities
{
    public class MemorySequenceComparer : IEqualityComparer<ReadOnlyMemory<byte>>, IComparer<ReadOnlyMemory<byte>>
    {
        public static MemorySequenceComparer Default { get; } = new MemorySequenceComparer(false);
        public static MemorySequenceComparer Reverse { get; } = new MemorySequenceComparer(true);

        private readonly bool reverse;

        private MemorySequenceComparer(bool reverse = false)
        {
            this.reverse = reverse;
        }

        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y) => Equals(x.Span, y.Span);

        public int GetHashCode(ReadOnlyMemory<byte> obj) => GetHashCode(obj.Span);

        public int Compare(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
        {
            var compare = x.Span.SequenceCompareTo(y.Span);
            return reverse ? -compare : compare;
        }

        public static bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y) => x.SequenceEqual(y);

        public static int GetHashCode(ReadOnlySpan<byte> span)
        {
            HashCode hash = default;
            hash.AddBytes(span);
            return hash.ToHashCode();
        }
    }
}
