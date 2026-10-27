// Copyright (C) 2015-2026 The Neo Project.
//
// MemoryCacheClientTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace test.bctklib
{
    public class MemoryCacheClientTests
    {
        static readonly UInt160 CONTRACT_A = UInt160.Parse("0x0000000000000000000000000000000000000001");
        static readonly UInt160 CONTRACT_B = UInt160.Parse("0x0000000000000000000000000000000000000002");

        [Fact]
        public void distinct_keys_that_collide_under_a_32bit_hash_keep_their_own_values()
        {
            // The cache previously used HashCode.ToHashCode() over (contract, key) as the
            // dictionary key, so two distinct storage keys whose 32-bit hashes collide served
            // each other's value. Search for such a colliding pair (birthday bound makes this
            // fast) and prove each key reads back its own value.
            var (first, second) = FindCollidingKeys(CONTRACT_A);
            using var client = new StateServiceStore.MemoryCacheClient();

            client.CacheStorage(CONTRACT_A, first, new byte[] { 1 });
            client.CacheStorage(CONTRACT_A, second, new byte[] { 2 });

            client.TryGetCachedStorage(CONTRACT_A, first, out var firstValue).Should().BeTrue();
            client.TryGetCachedStorage(CONTRACT_A, second, out var secondValue).Should().BeTrue();
            firstValue.Should().Equal(new byte[] { 1 });
            secondValue.Should().Equal(new byte[] { 2 });
        }

        [Fact]
        public void the_same_key_under_different_contracts_keeps_separate_values()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            var key = new byte[] { 0x0a, 0x0b };

            client.CacheStorage(CONTRACT_A, key, new byte[] { 1 });
            client.CacheStorage(CONTRACT_B, key, new byte[] { 2 });

            client.TryGetCachedStorage(CONTRACT_A, key, out var valueA).Should().BeTrue();
            client.TryGetCachedStorage(CONTRACT_B, key, out var valueB).Should().BeTrue();
            valueA.Should().Equal(new byte[] { 1 });
            valueB.Should().Equal(new byte[] { 2 });
        }

        [Fact]
        public void a_cached_key_is_matched_by_content_not_by_buffer_identity()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            var buffer = new byte[] { 0x0a, 0x0b };

            client.CacheStorage(CONTRACT_A, buffer, new byte[] { 1 });
            // Mutating the caller's buffer after caching must not corrupt the cached identity.
            buffer[0] = 0xff;

            client.TryGetCachedStorage(CONTRACT_A, new byte[] { 0x0a, 0x0b }, out var value).Should().BeTrue();
            value.Should().Equal(new byte[] { 1 });
            client.TryGetCachedStorage(CONTRACT_A, new byte[] { 0xff, 0x0b }, out _).Should().BeFalse();
        }

        [Fact]
        public void found_states_are_kept_per_contract_and_prefix()
        {
            using var client = new StateServiceStore.MemoryCacheClient();

            using (var snapshot = client.GetFoundStatesSnapshot(CONTRACT_A, 1))
            {
                snapshot.Add(new byte[] { 1 }, new byte[] { 10 });
                snapshot.Commit();
            }
            using (var snapshot = client.GetFoundStatesSnapshot(CONTRACT_A, 2))
            {
                snapshot.Add(new byte[] { 2 }, new byte[] { 20 });
                snapshot.Commit();
            }

            client.TryGetCachedFoundStates(CONTRACT_A, 1, out var prefix1).Should().BeTrue();
            client.TryGetCachedFoundStates(CONTRACT_A, 2, out var prefix2).Should().BeTrue();
            client.TryGetCachedFoundStates(CONTRACT_B, 1, out _).Should().BeFalse();
            Assert.Single(prefix1);
            Assert.Single(prefix2);
        }

        [Fact]
        public async Task concurrent_cache_writes_do_not_throw_and_keep_a_consistent_value()
        {
            // Two readers fetching the same storage key or found-states prefix concurrently
            // used to race: the loser of CacheStorage.TryAdd / Snapshot.Commit threw, which
            // surfaced as a random RPC failure. The caches now publish idempotently: the first
            // complete result wins and concurrent duplicates are dropped silently.
            var key = new byte[] { 0x0a, 0x0b, 0x0c };
            using var client = new StateServiceStore.MemoryCacheClient();

            var writers = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => client.CacheStorage(CONTRACT_A, key, new byte[] { 7 })))
                .ToArray();
            await Task.WhenAll(writers);

            client.TryGetCachedStorage(CONTRACT_A, key, out var cached).Should().BeTrue();
            cached.Should().Equal(new byte[] { 7 });

            var foundWriters = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() =>
                {
                    using var snapshot = client.GetFoundStatesSnapshot(CONTRACT_A, 3);
                    snapshot.Add(key, new byte[] { 9 });
                    snapshot.Commit();
                }))
                .ToArray();
            await Task.WhenAll(foundWriters);

            client.TryGetCachedFoundStates(CONTRACT_A, 3, out var found).Should().BeTrue();
            found.Should().ContainSingle();
            found.Single().value.Should().Equal(new byte[] { 9 });
        }

        [Fact]
        public void caching_null_does_not_throw_and_reads_back_as_cached_null()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            var key = new byte[] { 1 };

            client.Invoking(c => c.CacheStorage(CONTRACT_A, key, null)).Should().NotThrow();

            client.TryGetCachedStorage(CONTRACT_A, key, out var value).Should().BeTrue();
            value.Should().BeNull();
        }

        [Fact]
        public void uncached_key_returns_false_and_null()
        {
            using var client = new StateServiceStore.MemoryCacheClient();

            client.TryGetCachedStorage(CONTRACT_A, new byte[] { 1 }, out var value).Should().BeFalse();
            value.Should().BeNull();
        }

        [Fact]
        public void cached_empty_array_is_not_null()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            var key = new byte[] { 1 };

            client.CacheStorage(CONTRACT_A, key, Array.Empty<byte>());

            client.TryGetCachedStorage(CONTRACT_A, key, out var value).Should().BeTrue();
            value.Should().NotBeNull();
            value.Should().BeEmpty();
        }

        [Fact]
        public void mutating_input_value_does_not_change_cached_value()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            var key = new byte[] { 1 };
            var input = new byte[] { 1, 2, 3 };

            client.CacheStorage(CONTRACT_A, key, input);
            input[0] = 99;

            client.TryGetCachedStorage(CONTRACT_A, key, out var value).Should().BeTrue();
            value.Should().Equal(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void mutating_returned_value_does_not_change_cached_value()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            var key = new byte[] { 1 };
            client.CacheStorage(CONTRACT_A, key, new byte[] { 1, 2, 3 });

            client.TryGetCachedStorage(CONTRACT_A, key, out var first).Should().BeTrue();
            first![0] = 99;

            client.TryGetCachedStorage(CONTRACT_A, key, out var second).Should().BeTrue();
            second.Should().Equal(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void equal_content_keys_from_different_arrays_resolve_to_same_entry_first_writer_wins()
        {
            using var client = new StateServiceStore.MemoryCacheClient();

            client.CacheStorage(CONTRACT_A, new byte[] { 1, 2 }, new byte[] { 1 });
            client.CacheStorage(CONTRACT_A, new byte[] { 1, 2 }, new byte[] { 2 });

            client.TryGetCachedStorage(CONTRACT_A, new byte[] { 1, 2 }, out var value).Should().BeTrue();
            value.Should().Equal(new byte[] { 1 });
        }

        [Fact]
        public async Task concurrent_null_and_non_null_writes_publish_one_consistent_value()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            var key = new byte[] { 5 };

            var writers = Enumerable.Range(0, 16)
                .Select(i => Task.Run(() => client.CacheStorage(CONTRACT_A, key, i % 2 == 0 ? null : new byte[] { 7 })))
                .ToArray();
            await Task.WhenAll(writers);

            client.TryGetCachedStorage(CONTRACT_A, key, out var first).Should().BeTrue();
            client.TryGetCachedStorage(CONTRACT_A, key, out var second).Should().BeTrue();
            if (first is null)
                second.Should().BeNull();
            else
            {
                first.Should().Equal(new byte[] { 7 });
                second.Should().Equal(first);
            }
        }

        [Fact]
        public void disposed_cache_throws_object_disposed()
        {
            var client = new StateServiceStore.MemoryCacheClient();
            client.Dispose();
            var key = new byte[] { 1 };

            client.Invoking(c => c.CacheStorage(CONTRACT_A, key, null)).Should().Throw<ObjectDisposedException>();
            client.Invoking(c => c.TryGetCachedStorage(CONTRACT_A, key, out _)).Should().Throw<ObjectDisposedException>();
        }

        // Replicates the hash the cache previously used
        // distinct keys that collide under it. ~80k random keys hit a 32-bit collision with
        // high probability (birthday bound); the loop is capped well above that.
        static (byte[] first, byte[] second) FindCollidingKeys(UInt160 contract)
        {
            var seen = new Dictionary<int, byte[]>();
            for (var i = 0; i < 5_000_000; i++)
            {
                var key = Encoding.UTF8.GetBytes($"key-{i}");
                var hashBuilder = new HashCode();
                hashBuilder.Add(contract);
                hashBuilder.AddBytes(key);
                var hash = hashBuilder.ToHashCode();
                if (seen.TryGetValue(hash, out var existing))
                    return (existing, key);
                seen.Add(hash, key);
            }
            throw new Exception("No 32-bit hash collision found within the search bound");
        }
    }
}
