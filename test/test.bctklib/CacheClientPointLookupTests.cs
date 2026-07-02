// Copyright (C) 2015-2026 The Neo Project.
//
// CacheClientPointLookupTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Utilities;
using System;
using System.Linq;
using Xunit;

namespace test.bctklib
{
    using static Utility;

    public class CacheClientPointLookupTests
    {
        static readonly UInt160 CONTRACT = UInt160.Parse("0x0000000000000000000000000000000000000001");

        static void SeedFoundStates(ICacheClient client, byte? prefix, params (byte[] key, byte[] value)[] records)
        {
            using var snapshot = client.GetFoundStatesSnapshot(CONTRACT, prefix);
            foreach (var (key, value) in records)
            {
                snapshot.Add(key, value);
            }
            snapshot.Commit();
        }

        static void AssertPointLookupMatchesEnumeration(ICacheClient client, byte? prefix)
        {
            var records = new[]
            {
                (key: new byte[] { 1, 10 }, value: new byte[] { 100 }),
                (key: new byte[] { 1, 20 }, value: new byte[] { 101 }),
                (key: new byte[] { 1, 30 }, value: new byte[] { 102 }),
            };

            // before the record set is cached, the point lookup cannot answer
            client.TryGetCachedState(CONTRACT, prefix, records[0].key, out _).Should().BeFalse();

            SeedFoundStates(client, prefix, records);

            // hits: every record resolves and matches the enumerating API
            client.TryGetCachedFoundStates(CONTRACT, prefix, out var enumerated).Should().BeTrue();
            var byEnumeration = enumerated.ToArray();
            foreach (var (key, value) in records)
            {
                client.TryGetCachedState(CONTRACT, prefix, key, out var pointValue).Should().BeTrue();
                pointValue.Should().Equal(value);
                byEnumeration.Single(kvp => MemorySequenceComparer.Equals(kvp.key.Span, key)).value.Should().Equal(value);
            }

            // definitive miss: the set is cached but the key is absent
            client.TryGetCachedState(CONTRACT, prefix, new byte[] { 9, 9 }, out var missing).Should().BeTrue();
            missing.Should().BeNull();
        }

        [Fact]
        public void memory_cache_point_lookup_matches_enumeration()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            AssertPointLookupMatchesEnumeration(client, null);
        }

        [Fact]
        public void memory_cache_point_lookup_matches_enumeration_with_prefix()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            AssertPointLookupMatchesEnumeration(client, 1);
        }

        [Fact]
        public void rocksdb_cache_point_lookup_matches_enumeration()
        {
            using var path = new CleanupPath();
            using var db = RocksDbUtility.OpenDb(path);
            using var client = new StateServiceStore.RocksDbCacheClient(db, shared: true, nameof(CacheClientPointLookupTests));
            AssertPointLookupMatchesEnumeration(client, null);
        }

        [Fact]
        public void rocksdb_cache_point_lookup_matches_enumeration_with_prefix()
        {
            using var path = new CleanupPath();
            using var db = RocksDbUtility.OpenDb(path);
            using var client = new StateServiceStore.RocksDbCacheClient(db, shared: true, nameof(CacheClientPointLookupTests));
            AssertPointLookupMatchesEnumeration(client, 1);
        }

        [Fact]
        public void prefixed_and_unprefixed_record_sets_stay_separate()
        {
            using var client = new StateServiceStore.MemoryCacheClient();
            SeedFoundStates(client, 1, (new byte[] { 1, 10 }, new byte[] { 100 }));

            client.TryGetCachedState(CONTRACT, 1, new byte[] { 1, 10 }, out var hit).Should().BeTrue();
            hit.Should().Equal(new byte[] { 100 });
            // the null-prefix record set was never cached, so it cannot answer
            client.TryGetCachedState(CONTRACT, null, new byte[] { 1, 10 }, out _).Should().BeFalse();
        }
    }
}
