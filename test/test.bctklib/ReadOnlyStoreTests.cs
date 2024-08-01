// Copyright (C) 2015-2024 The Neo Project.
//
// ReadOnlyStoreTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Utilities;
using Neo.Persistence;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;

namespace test.bctklib;

using static Utility;

[SuppressMessage("IClassFixture", "xUnit1033")]
public class ReadOnlyStoreTests : IClassFixture<CheckpointFixture>, IClassFixture<RocksDbFixture>, IDisposable
{
    public enum StoreType { Checkpoint, Memory, NeoRocksDb, RocksDb }

    readonly CheckpointFixture checkpointFixture;
    readonly RocksDbFixture rocksDbFixture;
    readonly CleanupPath path = new();

    public ReadOnlyStoreTests(RocksDbFixture rocksDbFixture, CheckpointFixture checkpointFixture)
    {
        this.checkpointFixture = checkpointFixture;
        this.rocksDbFixture = rocksDbFixture;
    }

    public void Dispose()
    {
        path.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void checkpoint_cleans_up_on_dispose()
    {
        var checkpoint = new CheckpointStore(checkpointFixture.CheckpointPath);
        System.IO.Directory.Exists(checkpoint.CheckpointTempPath).Should().BeTrue();
        checkpoint.Dispose();
        System.IO.Directory.Exists(checkpoint.CheckpointTempPath).Should().BeFalse();
    }

    [Fact]
    public void checkpoint_settings()
    {
        using var store = new CheckpointStore(checkpointFixture.CheckpointPath);
        store.Settings.AddressVersion.Should().Be(checkpointFixture.AddressVersion);
        store.Settings.Network.Should().Be(checkpointFixture.Network);
    }

    [Fact]
    public void checkpoint_store_throws_on_incorrect_metadata()
    {
        Assert.Throws<Exception>(() => new CheckpointStore(checkpointFixture.CheckpointPath, addressVersion: 0));
        Assert.Throws<Exception>(() => new CheckpointStore(checkpointFixture.CheckpointPath, scriptHash: Neo.UInt160.Zero));
    }

    [Fact]
    public void readonly_rocksdb_store_throws_on_write_operations()
    {
        using (var popDB = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(popDB);
        }

        using var store = new RocksDbStore(RocksDbUtility.OpenReadOnlyDb(path), readOnly: true);
        Assert.Throws<InvalidOperationException>(() => store.Put(Bytes(0), Bytes(0)));
        Assert.Throws<InvalidOperationException>(() => store.PutSync(Bytes(0), Bytes(0)));
        Assert.Throws<InvalidOperationException>(() => store.Delete(Bytes(0)));
        Assert.Throws<InvalidOperationException>(() => store.GetSnapshot());
    }

    [Fact]
    public void rocksdb_store_sharing()
    {
        using (var popDB = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(popDB);
        }

        using var db = RocksDbUtility.OpenDb(path);
        var column = db.GetDefaultColumnFamily();
        TestStoreSharing(shared => new RocksDbStore(db, column, readOnly: true, shared));
    }

    [Fact]
    public void persistent_tracking_store_sharing()
    {
        var memoryStore = new MemoryStore();
        foreach (var (key, value) in TestData)
        {
            memoryStore.Put(key, value);
        }

        using var db = RocksDbUtility.OpenDb(path);
        var column = db.GetDefaultColumnFamily();
        TestStoreSharing(shared => new PersistentTrackingStore(db, column, memoryStore, shared));
    }


    [Theory, CombinatorialData]
    public void tryget_value_for_valid_key(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_tryget_value_for_valid_key(store);
    }

    internal static void test_tryget_value_for_valid_key(IReadOnlyStore store, int index = 0)
    {
        using var _ = store as IDisposable;
        var (key, value) = TestData.ElementAt(index);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    [Theory, CombinatorialData]
    public void tryget_null_for_missing_value(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_tryget_null_for_missing_value(store);
    }

    internal static void test_tryget_null_for_missing_value(IReadOnlyStore store)
    {
        using var _ = store as IDisposable;
        store.TryGet(Bytes(0)).Should().BeNull();
    }

    [Theory, CombinatorialData]
    public void contains_true_for_valid_key(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_contains_true_for_valid_key(store);
    }

    internal static void test_contains_true_for_valid_key(IReadOnlyStore store, int index = 0)
    {
        using var _ = store as IDisposable;
        var (key, value) = TestData.ElementAt(index);
        store.Contains(key).Should().BeTrue();
    }

    [Theory, CombinatorialData]
    public void contains_false_for_missing_key(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_contains_false_for_missing_key(store);
    }


    internal static void test_contains_false_for_missing_key(IReadOnlyStore store)
    {
        using var _ = store as IDisposable;
        store.Contains(Bytes(0)).Should().BeFalse();
    }

    [Theory, CombinatorialData]
    public void can_seek_forward_no_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_can_seek_forward_no_prefix(store);
    }

    internal static void test_can_seek_forward_no_prefix(IReadOnlyStore store)
    {
        using var _ = store as IDisposable;
        store.Seek(Array.Empty<byte>(), SeekDirection.Forward)
            .Should().BeEquivalentTo(TestData);
    }

    [Theory, CombinatorialData]
    public void can_seek_backwards_no_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_can_seek_backwards_no_prefix(store);
    }

    internal static void test_can_seek_backwards_no_prefix(IReadOnlyStore store)
    {
        using var _ = store as IDisposable;
        store.Seek(Array.Empty<byte>(), SeekDirection.Backward).Should().BeEmpty();
    }

    [Theory, CombinatorialData]
    public void seek_forwards_with_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_seek_forwards_with_prefix(store);
    }

    internal static void test_seek_forwards_with_prefix(IReadOnlyStore store)
    {
        using var _ = store as IDisposable;
        var key = new byte[] { 1, 0 };
        var expected = TestData
            .Where(kvp => MemorySequenceComparer.Default.Compare(kvp.key, key) >= 0);
        store.Seek(key, SeekDirection.Forward).Should().BeEquivalentTo(expected);
    }

    [Theory, CombinatorialData]
    public void seek_backwards_with_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_seek_backwards_with_prefix(store);
    }

    internal static void test_seek_backwards_with_prefix(IReadOnlyStore store)
    {
        using var _ = store as IDisposable;
        var key = new byte[] { 2, 0 };
        var expected = TestData
            .Where(kvp => MemorySequenceComparer.Reverse.Compare(kvp.key, key) >= 0)
            .Reverse();
        store.Seek(key, SeekDirection.Backward).Should().BeEquivalentTo(expected);
    }

    IReadOnlyStore GetStore(StoreType type) => type switch
    {
        StoreType.Checkpoint => new CheckpointStore(checkpointFixture.CheckpointPath),
        StoreType.Memory => GetPopulatedMemoryStore(),
        StoreType.NeoRocksDb => CreateNeoRocksDb(rocksDbFixture.DbPath),
        StoreType.RocksDb => new RocksDbStore(RocksDbUtility.OpenDb(rocksDbFixture.DbPath), readOnly: true),
        _ => throw new Exception($"Invalid {nameof(StoreType)}")
    };

    internal static MemoryStore GetPopulatedMemoryStore()
    {
        MemoryStore memoryStore = new();
        foreach (var (key, value) in TestData)
        {
            memoryStore.Put(key, value);
        }
        return memoryStore;
    }

    static void TestStoreSharing(Func<bool, IStore> storeFactory)
    {
        var (key, value) = TestData.First();

        // create shared, doesn't dispose underlying rocksDB when store disposed
        var store1 = storeFactory(true);
        store1.TryGet(key).Should().BeEquivalentTo(value);
        store1.Dispose();

        // create unshared, disposes underlying rocksDB when store disposed
        var store2 = storeFactory(false);
        store2.TryGet(key).Should().BeEquivalentTo(value);
        store2.Dispose();
        Assert.Throws<ObjectDisposedException>(() => store2.TryGet(key));

        // since underlying rocksDB is disposed, attempting to call methods
        // on a new instance will throw
        using var store3 = storeFactory(true);
        Assert.Throws<ObjectDisposedException>(() => store3.TryGet(key));
    }
}
