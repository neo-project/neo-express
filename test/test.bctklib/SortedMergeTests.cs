// Copyright (C) 2015-2026 The Neo Project.
//
// SortedMergeTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Utilities;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace test.bctklib
{
    public class SortedMergeTests
    {
        static (byte[] Key, byte[] Value) Item(byte key) => (new[] { key }, new[] { key });

        [Fact]
        public void merges_two_forward_sorted_sequences_into_one()
        {
            var first = new[] { Item(1), Item(4), Item(7) };
            var second = new[] { Item(2), Item(3), Item(8) };

            var merged = first.MergeSorted(second, MemorySequenceComparer.Default).ToArray();

            merged.Select(kvp => kvp.Key[0]).Should().Equal(1, 2, 3, 4, 7, 8);
        }

        [Fact]
        public void merges_two_backward_sorted_sequences_into_one()
        {
            var first = new[] { Item(7), Item(4), Item(1) };
            var second = new[] { Item(8), Item(3), Item(2) };

            var merged = first.MergeSorted(second, MemorySequenceComparer.Reverse).ToArray();

            merged.Select(kvp => kvp.Key[0]).Should().Equal(8, 7, 4, 3, 2, 1);
        }

        [Fact]
        public void equal_keys_yield_the_first_sequence_item_first()
        {
            var first = new[] { (Key: new byte[] { 5 }, Value: new byte[] { 100 }) };
            var second = new[] { (Key: new byte[] { 5 }, Value: new byte[] { 200 }) };

            var merged = first.MergeSorted(second, MemorySequenceComparer.Default).ToArray();

            merged.Should().HaveCount(2);
            merged[0].Value.Should().Equal(new byte[] { 100 });
        }

        [Fact]
        public void consuming_a_prefix_does_not_enumerate_the_second_sequence_to_its_end()
        {
            var pulled = 0;
            IEnumerable<(byte[] Key, byte[] Value)> Counting()
            {
                for (byte i = 0; i < 100; i++)
                {
                    pulled++;
                    yield return Item(i);
                }
            }

            var first = new[] { Item(1), Item(3) };
            _ = first.MergeSorted(Counting(), MemorySequenceComparer.Default).Take(4).ToArray();

            // Concat+OrderBy would have pulled all 100; the merge reads only what it needs
            // (plus single-item look-ahead).
            pulled.Should().BeLessThan(6);
        }

        [Fact]
        public void tracking_store_find_no_longer_walks_the_backing_store_to_its_end()
        {
            var backing = new CountingStore(Enumerable.Range(0, 1000)
                .Select(i => (Key: new[] { (byte)(i / 256), (byte)(i % 256) }, Value: new byte[] { 1 }))
                .ToArray());
            using var store = new MemoryTrackingStore(backing);
            store.Put(new byte[] { 0, 10 }, new byte[] { 2 });

            var taken = store.Find(Array.Empty<byte>(), SeekDirection.Forward).Take(5).ToArray();

            taken.Should().HaveCount(5);
            backing.ItemsPulled.Should().BeLessThan(20,
                "a prefixed Find taking 5 items must not enumerate all 1000 backing entries");
        }

        sealed class CountingStore : IReadOnlyStore<byte[], byte[]>
        {
            readonly (byte[] Key, byte[] Value)[] items;
            public int ItemsPulled { get; private set; }

            public CountingStore((byte[] Key, byte[] Value)[] items)
            {
                this.items = items;
            }

            public byte[] this[byte[] key] => TryGet(key, out var value)
                ? value!
                : throw new KeyNotFoundException();

            [Obsolete("use TryGet(byte[] key, out byte[]? value) instead.")]
            public byte[]? TryGet(byte[] key) => TryGet(key, out var value) ? value : null;

            public bool TryGet(byte[] key, out byte[]? value)
            {
                foreach (var (k, v) in items)
                {
                    if (k.AsSpan().SequenceEqual(key))
                    {
                        value = v;
                        return true;
                    }
                }
                value = null;
                return false;
            }

            public bool Contains(byte[] key) => TryGet(key, out _);

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                var comparer = direction == SeekDirection.Forward
                    ? MemorySequenceComparer.Default
                    : MemorySequenceComparer.Reverse;
                var ordered = items
                    .Where(kvp => key_prefix is null || key_prefix.Length == 0 || comparer.Compare(kvp.Key, key_prefix) >= 0)
                    .OrderBy(kvp => kvp.Key, comparer);
                foreach (var item in ordered)
                {
                    ItemsPulled++;
                    yield return item;
                }
            }
        }
    }
}
