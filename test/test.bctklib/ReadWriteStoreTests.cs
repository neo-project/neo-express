// Copyright (C) 2015-2024 The Neo Project.
//
// ReadWriteStoreTests.cs file belongs to neo-express project and is free
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
using System;
using System.Linq;
using Xunit;

namespace test.bctklib;

using static Utility;

public class ReadWriteStoreTests : IDisposable
{
    // include Neo.Persistence MemoryStore and Neo.Plugins.Storage.RocksDBStore for comparison
    public enum StoreType { Memory, NeoRocksDb, RocksDb }

    readonly CleanupPath path = new CleanupPath();

    public void Dispose()
    {
        path.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory, CombinatorialData]
    public void put_new_value(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_put_new_value(store);
    }

    internal static void test_put_new_value(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.TryGet(key).Should().BeNull();
        store.Put(key, value);
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    [Theory, CombinatorialData]
    public void put_overwrite_existing_value(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_put_overwrite_existing_value(store);
    }

    internal static void test_put_overwrite_existing_value(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.TryGet(key).Should().BeEquivalentTo(value);
        store.Put(key, newValue);
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    [Theory, CombinatorialData]
    public void tryget_return_null_for_deleted_key(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_tryget_return_null_for_deleted_key(store);
    }

    internal static void test_tryget_return_null_for_deleted_key(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        store.TryGet(key).Should().BeEquivalentTo(value);
        store.Delete(key);
        store.TryGet(key).Should().BeNull();
    }

    [Theory, CombinatorialData]
    public void contains_false_for_deleted_key(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_contains_false_for_deleted_key(store);
    }

    internal static void test_contains_false_for_deleted_key(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        store.Contains(key).Should().BeTrue();
        store.Delete(key);
        store.Contains(key).Should().BeFalse();
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_add(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_add(store);
    }

    internal static void test_snapshot_commit_add(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        store.TryGet(key).Should().BeNull();
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(value);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_update(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_update(store);
    }

    internal static void test_snapshot_commit_update(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, newValue);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeEquivalentTo(newValue);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_delete(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_delete(store);
    }

    internal static void test_snapshot_commit_delete(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        using var snapshot = store.GetSnapshot();
        snapshot.Delete(key);

        store.TryGet(key).Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key).Should().BeNull();
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_addition(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_addition(store);
    }

    internal static void test_snapshot_isolation_addition(IStore store)
    {
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeFalse();
        snapshot.TryGet(key).Should().BeNull();
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_update(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_update(store);
    }

    internal static void test_snapshot_isolation_update(IStore store, int index = 0)
    {
        using var snapshot = store.GetSnapshot();
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");
        store.Put(key, newValue);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_delete(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_delete(store);
    }

    internal static void test_snapshot_isolation_delete(IStore store, int index = 0)
    {
        using var snapshot = store.GetSnapshot();
        var (key, value) = TestData.ElementAt(index);
        store.Delete(key);
        snapshot.Contains(key).Should().BeTrue();
        snapshot.TryGet(key).Should().BeEquivalentTo(value);
    }

    [Theory, CombinatorialData]
    public void key_instance_isolation(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_key_instance_isolation(store);
    }

    internal static void test_key_instance_isolation(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.Put(key, value);

        key[0] = 0xff;
        store.TryGet(Bytes(0)).Should().BeEquivalentTo(value);
        store.TryGet(key).Should().BeNull();
    }

    [Theory, CombinatorialData]
    public void value_instance_isolation(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_value_instance_isolation(store);
    }

    internal static void test_value_instance_isolation(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        store.Put(key, value);

        value[0] = 0xff;
        store.TryGet(key).Should().BeEquivalentTo(Bytes("test-value"));
    }

    [Theory, CombinatorialData]
    public void put_null_value_throws(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_put_null_value_throws(store);
    }

    internal static void test_put_null_value_throws(IStore store)
    {
        var key = Bytes(0);
        AssertThrowsNullCheck(() => store.Put(key, null));
    }

    [Theory, CombinatorialData]
    public void snapshot_put_null_value_throws(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_put_null_value_throws(store);
    }

    internal static void test_snapshot_put_null_value_throws(IStore store)
    {
        using var snapshot = store.GetSnapshot();
        var key = Bytes(0);
        AssertThrowsNullCheck(() => snapshot.Put(key, null));
    }

    static void AssertThrowsNullCheck(Action testCode)
    {
        // MemoryStore throws ArgumentNullException instead of NullReferenceException
        // For test purposes, consider both valid

        var ex = Assert.ThrowsAny<Exception>(testCode);
        Assert.True(ex.GetType() == typeof(NullReferenceException)
            || ex.GetType() == typeof(ArgumentNullException));
    }

    [Theory, CombinatorialData]
    public void delete_missing_value_no_effect(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_delete_missing_value_no_effect(store);
    }

    internal static void test_delete_missing_value_no_effect(IStore store)
    {
        var key = Bytes(0);
        store.TryGet(key).Should().BeNull();
        store.Delete(key);
        store.TryGet(key).Should().BeNull();
    }

    IStore GetStore(StoreType type) => type switch
    {
        StoreType.Memory => ReadOnlyStoreTests.GetPopulatedMemoryStore(),
        StoreType.RocksDb => GetPopulatedRocksDbStore(path),
        StoreType.NeoRocksDb => GetPopulatedNeoRocksDbStore(path),
        _ => throw new Exception($"Invalid {nameof(StoreType)}"),
    };

    public static RocksDbStore GetPopulatedRocksDbStore(string path)
    {
        using (var db = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(db);
        }

        return new RocksDbStore(RocksDbUtility.OpenDb(path));
    }

    public static IStore GetPopulatedNeoRocksDbStore(string path)
    {
        using (var db = RocksDbUtility.OpenDb(path))
        {
            RocksDbFixture.Populate(db);
        }

        var storeType = typeof(Neo.Plugins.Storage.RocksDBStore).Assembly
            .GetType("Neo.Plugins.Storage.Store");
        var storeCtor = storeType?.GetConstructor(new[] { typeof(string) });
        var store = storeCtor?.Invoke(new object[] { path }) as IStore;
        if (store is null)
            throw new NullReferenceException(nameof(Neo.Plugins.Storage.RocksDBStore));
        return store;
    }
}
