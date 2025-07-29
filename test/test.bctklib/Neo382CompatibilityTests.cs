// Copyright (C) 2015-2025 The Neo Project.
//
// Neo382CompatibilityTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using System.Text;
using Xunit;

namespace test.bctklib;

using static Utility;

public class Neo382CompatibilityTests
{
    [Fact]
    public void RocksDbStore_Should_Support_Neo382_Interface()
    {
        using var tempPath = new CleanupPath();
        using var store = RocksDbUtility.OpenDb(tempPath.Path);
        using var rocksDbStore = new RocksDbStore(store);

        // Test legacy interface
        TestLegacyInterface(rocksDbStore);

        // Test Neo 3.8.2 interface
        TestNeo382Interface(rocksDbStore);
    }

    [Fact]
    public void MemoryTrackingStore_Should_Support_Neo382_Interface()
    {
        using var store = new MemoryTrackingStore(NullStore.Instance);

        // Test legacy interface
        TestLegacyInterface(store);

        // Test Neo 3.8.2 interface
        TestNeo382Interface(store);
    }

    [Fact]
    public void PersistentTrackingStore_Should_Support_Neo382_Interface()
    {
        using var tempPath = new CleanupPath();
        using var db = RocksDbUtility.OpenDb(tempPath.Path);
        using var store = new PersistentTrackingStore(db, NullStore.Instance);

        // Test legacy interface
        TestLegacyInterface(store);

        // Test Neo 3.8.2 interface
        TestNeo382Interface(store);
    }



    private static void TestLegacyInterface(IStore store)
    {
        var testKey = Encoding.UTF8.GetBytes("legacy-test");
        var testValue = Encoding.UTF8.GetBytes("legacy-value");

        // Test store methods
        store.Put(testKey, testValue);
        store.Contains(testKey).Should().BeTrue();
        store.TryGet(testKey, out var retrievedValue).Should().BeTrue();
        retrievedValue.Should().BeEquivalentTo(testValue);

        // Test snapshot methods
        using var snapshot = store.GetSnapshot();
        snapshot.Should().NotBeNull();
        snapshot.Store.Should().Be(store);
        snapshot.Contains(testKey).Should().BeTrue();
        snapshot.TryGet(testKey, out var snapshotValue).Should().BeTrue();
        snapshotValue.Should().BeEquivalentTo(testValue);

        var newKey = Encoding.UTF8.GetBytes("snapshot-test");
        var newValue = Encoding.UTF8.GetBytes("snapshot-value");
        snapshot.Put(newKey, newValue);
        snapshot.Contains(newKey).Should().BeTrue();
        snapshot.Commit();

        store.Contains(newKey).Should().BeTrue();
    }

    private static void TestNeo382Interface(IStore store)
    {
        var testKey = Encoding.UTF8.GetBytes("neo382-test");
        var testValue = Encoding.UTF8.GetBytes("neo382-value");

        // Test store methods with Neo 3.8.2 interface
        store.Put(testKey, testValue);
        store.Contains(testKey).Should().BeTrue();
        store.Find(testKey).Should().NotBeEmpty();

        // Test Neo 3.8.2 snapshot
        using var storeSnapshot = store.GetSnapshot();
        storeSnapshot.Should().NotBeNull();
        storeSnapshot.Store.Should().Be(store);
        storeSnapshot.Contains(testKey).Should().BeTrue();
        storeSnapshot.TryGet(testKey, out var snapshotValue).Should().BeTrue();
        snapshotValue.Should().BeEquivalentTo(testValue);

        var newKey = Encoding.UTF8.GetBytes("neo382-snapshot-test");
        var newValue = Encoding.UTF8.GetBytes("neo382-snapshot-value");
        storeSnapshot.Put(newKey, newValue);
        storeSnapshot.Contains(newKey).Should().BeTrue();
        storeSnapshot.Commit();

        store.Contains(newKey).Should().BeTrue();
    }
}
