// Copyright (C) 2015-2026 The Neo Project.
//
// ReadWriteStoreTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
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
        store.TryGet(key, out var initialValue).Should().BeFalse();
        initialValue.Should().BeNull();
        store.Put(key, value);
        store.TryGet(key, out var retrievedValue).Should().BeTrue();
        retrievedValue.Should().BeEquivalentTo(value);
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
        store.TryGet(key, out var initialValue).Should().BeTrue();
        initialValue.Should().BeEquivalentTo(value);
        store.Put(key, newValue);
        store.TryGet(key, out var retrievedValue).Should().BeTrue();
        retrievedValue.Should().BeEquivalentTo(newValue);
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
        store.TryGet(key, out var initialValue).Should().BeTrue();
        initialValue.Should().BeEquivalentTo(value);
        store.Delete(key);
        store.TryGet(key, out var deletedValue).Should().BeFalse();
        deletedValue.Should().BeNull();
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

        store.TryGet(key, out var beforeCommit).Should().BeFalse();
        beforeCommit.Should().BeNull();
        snapshot.Commit();
        store.TryGet(key, out var afterCommit).Should().BeTrue();
        afterCommit.Should().BeEquivalentTo(value);
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

        store.TryGet(key, out var beforeCommit).Should().BeTrue();
        beforeCommit.Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key, out var afterCommit).Should().BeTrue();
        afterCommit.Should().BeEquivalentTo(newValue);
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

        store.TryGet(key, out var beforeCommit).Should().BeTrue();
        beforeCommit.Should().BeEquivalentTo(value);
        snapshot.Commit();
        store.TryGet(key, out var afterCommit).Should().BeFalse();
        afterCommit.Should().BeNull();
    }

    [Fact]
    public void RocksDbSnapshotFindIncludesUncommittedPut()
    {
        using var store = GetPopulatedRocksDbStore(path);
        TestSnapshotFindIncludesUncommittedPut(store);
    }

    internal static void TestSnapshotFindIncludesUncommittedPut(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        AssertFindContains(snapshot, key, value);
    }

    [Fact]
    public void RocksDbSnapshotFindIncludesUncommittedUpdate()
    {
        using var store = GetPopulatedRocksDbStore(path);
        TestSnapshotFindIncludesUncommittedUpdate(store);
    }

    internal static void TestSnapshotFindIncludesUncommittedUpdate(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);
        var newValue = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, newValue);

        snapshot.TryGet(key, out var updatedValue).Should().BeTrue();
        updatedValue.Should().BeEquivalentTo(newValue);
        AssertFindContains(snapshot, key, newValue);
        AssertFindDoesNotContain(snapshot, key, value);
    }

    [Fact]
    public void RocksDbSnapshotFindExcludesUncommittedDelete()
    {
        using var store = GetPopulatedRocksDbStore(path);
        TestSnapshotFindExcludesUncommittedDelete(store);
    }

    internal static void TestSnapshotFindExcludesUncommittedDelete(IStore store, int index = 0)
    {
        var (key, value) = TestData.ElementAt(index);

        using var snapshot = store.GetSnapshot();
        snapshot.Delete(key);

        snapshot.TryGet(key, out var deletedValue).Should().BeFalse();
        deletedValue.Should().BeNull();
        AssertFindDoesNotContain(snapshot, key, value);
    }

    static void AssertFindContains(IStoreSnapshot snapshot, byte[] key, byte[] value)
    {
        snapshot.Find(Array.Empty<byte>(), SeekDirection.Forward)
            .Any(kvp => kvp.Key.SequenceEqual(key) && kvp.Value.SequenceEqual(value))
            .Should().BeTrue();
        snapshot.Find([byte.MaxValue], SeekDirection.Backward)
            .Any(kvp => kvp.Key.SequenceEqual(key) && kvp.Value.SequenceEqual(value))
            .Should().BeTrue();
    }

    static void AssertFindDoesNotContain(IStoreSnapshot snapshot, byte[] key, byte[] value)
    {
        snapshot.Find(Array.Empty<byte>(), SeekDirection.Forward)
            .Any(kvp => kvp.Key.SequenceEqual(key) && kvp.Value.SequenceEqual(value))
            .Should().BeFalse();
        snapshot.Find([byte.MaxValue], SeekDirection.Backward)
            .Any(kvp => kvp.Key.SequenceEqual(key) && kvp.Value.SequenceEqual(value))
            .Should().BeFalse();
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
        snapshot.TryGet(key, out var snapshotValue).Should().BeFalse();
        snapshotValue.Should().BeNull();
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
        snapshot.TryGet(key, out var snapshotValue).Should().BeTrue();
        snapshotValue.Should().BeEquivalentTo(value);
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
        snapshot.TryGet(key, out var snapshotValue).Should().BeTrue();
        snapshotValue.Should().BeEquivalentTo(value);
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
        store.TryGet(Bytes(0), out var originalValue).Should().BeTrue();
        originalValue.Should().BeEquivalentTo(value);
        store.TryGet(key, out var modifiedValue).Should().BeFalse();
        modifiedValue.Should().BeNull();
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
        store.TryGet(key, out var retrievedValue).Should().BeTrue();
        retrievedValue.Should().BeEquivalentTo(Bytes("test-value"));
    }

    [Fact]
    public void RocksDbSnapshotKeyInstanceIsolation()
    {
        using var store = GetStore(StoreType.RocksDb);
        TestSnapshotKeyInstanceIsolation(store);
    }

    internal static void TestSnapshotKeyInstanceIsolation(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        key[0] = 0xff;
        snapshot.TryGet(Bytes(0), out var snapshotValue).Should().BeTrue();
        snapshotValue.Should().BeEquivalentTo(value);

        snapshot.Commit();
        store.TryGet(Bytes(0), out var committedValue).Should().BeTrue();
        committedValue.Should().BeEquivalentTo(value);
    }

    [Fact]
    public void RocksDbSnapshotValueInstanceIsolation()
    {
        using var store = GetStore(StoreType.RocksDb);
        TestSnapshotValueInstanceIsolation(store);
    }

    internal static void TestSnapshotValueInstanceIsolation(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");
        var expected = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        value[0] = 0xff;
        snapshot.TryGet(key, out var snapshotValue).Should().BeTrue();
        snapshotValue.Should().BeEquivalentTo(expected);

        snapshot.Commit();
        store.TryGet(key, out var committedValue).Should().BeTrue();
        committedValue.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void RocksDbSnapshotDeleteKeyInstanceIsolation()
    {
        using var store = GetStore(StoreType.RocksDb);
        TestSnapshotDeleteKeyInstanceIsolation(store);
    }

    internal static void TestSnapshotDeleteKeyInstanceIsolation(IStore store, int index = 0)
    {
        var (key, _) = TestData.ElementAt(index);
        var expectedKey = key.ToArray();

        using var snapshot = store.GetSnapshot();
        snapshot.Delete(key);

        key[0] = 0xff;
        snapshot.TryGet(expectedKey, out var snapshotValue).Should().BeFalse();
        snapshotValue.Should().BeNull();

        snapshot.Commit();
        store.TryGet(expectedKey, out var committedValue).Should().BeFalse();
        committedValue.Should().BeNull();
    }

    [Fact]
    public void RocksDbSnapshotTryGetReflectsPendingInsert()
    {
        using var store = GetPopulatedRocksDbStore(path);
        test_snapshot_tryget_overloads_reflect_pending_insert(store);
    }

    [Fact]
    public void RocksDbSnapshotTryGetReflectsPendingUpdate()
    {
        using var store = GetPopulatedRocksDbStore(path);
        test_snapshot_tryget_overloads_reflect_pending_update(store);
    }

    [Fact]
    public void RocksDbSnapshotTryGetReflectsPendingDelete()
    {
        using var store = GetPopulatedRocksDbStore(path);
        test_snapshot_tryget_overloads_reflect_pending_delete(store);
    }

    [Fact]
    public void RocksDbSnapshotNullAndEmptyKeysAreEquivalent()
    {
        using var store = GetPopulatedRocksDbStore(path);
        test_snapshot_null_and_empty_keys_are_equivalent(store);
    }

    [Fact]
    public void RocksDbSnapshotTryGetReturnsIndependentBuffer()
    {
        using var store = GetPopulatedRocksDbStore(path);
        test_snapshot_tryget_returns_independent_buffer(store);
    }

    [Fact]
    public void RocksDbSnapshotCopiesPutInputs()
    {
        using var store = GetPopulatedRocksDbStore(path);
        var key = Bytes(0);
        var value = new byte[] { 0x42 };
        var expectedKey = key.ToArray();

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);
        key[0] = 0xff;
        value[0] = 0x77;

        snapshot.TryGet(expectedKey, out var result).Should().BeTrue();
        result.Should().Equal(new byte[] { 0x42 });
    }

    [Fact]
    public void RocksDbSnapshotDisposedTryGetThrows()
    {
        using var store = GetPopulatedRocksDbStore(path);
        var snapshot = store.GetSnapshot();
        snapshot.Dispose();

        Assert.Throws<ObjectDisposedException>(() => snapshot.TryGet(Bytes(0), out _));
#pragma warning disable CS0618
        Assert.Throws<ObjectDisposedException>(() => snapshot.TryGet(Bytes(0)));
#pragma warning restore CS0618
    }

    internal static void test_snapshot_tryget_overloads_reflect_pending_insert(IStore store)
    {
        var key = Bytes(0);
        var value = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, value);

        AssertTryGetBoth(snapshot, key, value);
        snapshot.Contains(key).Should().BeTrue();
        AssertFindContains(snapshot, key, value);
        store.Contains(key).Should().BeFalse();
    }

    internal static void test_snapshot_tryget_overloads_reflect_pending_update(IStore store)
    {
        var (key, original) = TestData.ElementAt(0);
        var newValue = Bytes("test-value");

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, newValue);

        AssertTryGetBoth(snapshot, key, newValue);
        store.TryGet(key, out var underlying).Should().BeTrue();
        underlying.Should().Equal(original);

        snapshot.Commit();
        store.TryGet(key, out var committed).Should().BeTrue();
        committed.Should().Equal(newValue);
    }

    internal static void test_snapshot_tryget_overloads_reflect_pending_delete(IStore store)
    {
        var (key, original) = TestData.ElementAt(0);

        using var snapshot = store.GetSnapshot();
        snapshot.Delete(key);

        snapshot.TryGet(key, out var value).Should().BeFalse();
        value.Should().BeNull();
#pragma warning disable CS0618
        snapshot.TryGet(key).Should().BeNull();
#pragma warning restore CS0618
        snapshot.Contains(key).Should().BeFalse();
        AssertFindDoesNotContain(snapshot, key, original);
        store.Contains(key).Should().BeTrue();

        snapshot.Commit();
        store.Contains(key).Should().BeFalse();
    }

    internal static void test_snapshot_null_and_empty_keys_are_equivalent(IStore store)
    {
        var value = Bytes("test-value");
        var empty = Array.Empty<byte>();

        using var snapshot = store.GetSnapshot();
        snapshot.Put(null!, value);
        AssertTryGetBoth(snapshot, null, value);
        AssertTryGetBoth(snapshot, empty, value);

        var updated = Bytes("updated");
        snapshot.Put(empty, updated);
        AssertTryGetBoth(snapshot, null, updated);

        snapshot.Delete(null!);
        snapshot.TryGet(empty, out var deleted).Should().BeFalse();
        deleted.Should().BeNull();
#pragma warning disable CS0618
        snapshot.TryGet(null!).Should().BeNull();
#pragma warning restore CS0618
    }

    internal static void test_snapshot_tryget_returns_independent_buffer(IStore store)
    {
        var key = Bytes(0);

        using var snapshot = store.GetSnapshot();
        snapshot.Put(key, new byte[] { 0x42 });

        snapshot.TryGet(key, out var first).Should().BeTrue();
        first![0] = 0x77;
#pragma warning disable CS0618
        var obsolete = snapshot.TryGet(key);
#pragma warning restore CS0618
        obsolete.Should().Equal(new byte[] { 0x42 });
        obsolete![0] = 0x78;

        snapshot.TryGet(key, out var second).Should().BeTrue();
        second.Should().Equal(new byte[] { 0x42 });
        AssertFindContains(snapshot, key, new byte[] { 0x42 });

        snapshot.Commit();
        store.TryGet(key, out var committed).Should().BeTrue();
        committed.Should().Equal(new byte[] { 0x42 });
    }

    static void AssertTryGetBoth(IStoreSnapshot snapshot, byte[]? key, byte[] expected)
    {
        snapshot.TryGet(key!, out var value).Should().BeTrue();
        value.Should().Equal(expected);
#pragma warning disable CS0618
        snapshot.TryGet(key!).Should().Equal(expected);
#pragma warning restore CS0618
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
        store.TryGet(key, out var initialValue).Should().BeFalse();
        initialValue.Should().BeNull();
        store.Delete(key);
        store.TryGet(key, out var afterDelete).Should().BeFalse();
        afterDelete.Should().BeNull();
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
