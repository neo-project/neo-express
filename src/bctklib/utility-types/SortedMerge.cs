// Copyright (C) 2015-2026 The Neo Project.
//
// SortedMerge.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.BlockchainToolkit.Utilities
{
    static class SortedMerge
    {
        // Lazily merge two sequences that are each already sorted by key under the given
        // comparer. Unlike Concat+OrderBy — which buffers and sorts both sequences in full
        // on the first pull — this enumerates each side only as far as the caller consumes,
        // so a prefixed Find that takes a handful of items no longer walks the backing
        // store to its end. Ties yield the first sequence's item first; the tracking-store
        // callers exclude duplicate keys between the sides, so ties do not occur there.
        public static IEnumerable<(byte[] Key, byte[] Value)> MergeSorted(
            this IEnumerable<(byte[] Key, byte[] Value)> first,
            IEnumerable<(byte[] Key, byte[] Value)> second,
            IComparer<ReadOnlyMemory<byte>> comparer)
        {
            using var firstEnumerator = first.GetEnumerator();
            using var secondEnumerator = second.GetEnumerator();
            var hasFirst = firstEnumerator.MoveNext();
            var hasSecond = secondEnumerator.MoveNext();

            while (hasFirst && hasSecond)
            {
                if (comparer.Compare(firstEnumerator.Current.Key, secondEnumerator.Current.Key) <= 0)
                {
                    yield return firstEnumerator.Current;
                    hasFirst = firstEnumerator.MoveNext();
                }
                else
                {
                    yield return secondEnumerator.Current;
                    hasSecond = secondEnumerator.MoveNext();
                }
            }

            while (hasFirst)
            {
                yield return firstEnumerator.Current;
                hasFirst = firstEnumerator.MoveNext();
            }

            while (hasSecond)
            {
                yield return secondEnumerator.Current;
                hasSecond = secondEnumerator.MoveNext();
            }
        }
    }
}
