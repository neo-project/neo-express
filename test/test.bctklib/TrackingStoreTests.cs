// Copyright (C) 2015-2024 The Neo Project.
//
// TrackingStoreTests.cs file belongs to neo-express project and is free
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
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace test.bctklib;

using static ReadOnlyStoreTests;
using static ReadWriteStoreTests;
using static Utility;

public class TrackingStoreTests : IDisposable
{
    public enum StoreType { MemoryTracking, PersistentTracking }

    readonly CleanupPath path = new CleanupPath();

    public void Dispose()
    {
        path.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory, CombinatorialData]
    public void tracking_store_disposes_underlying_store(StoreType storeType)
    {
        var disposableStore = new DisposableStore();
        var trackingStore = GetTrackingStore(storeType, disposableStore);
        disposableStore.Disposed.Should().BeFalse();
        trackingStore.Dispose();
        disposableStore.Disposed.Should().BeTrue();
    }

    // index combinatorial enables test of three different scenarios:
    //  * all even indexes (including zero) have a value in the underlying store with no updates in tracking store
    //  * all odd indexes (including 1 and 5) have an updated value in the tracking store
    //  * odd indexes that are also factors of 5 (including 5) have an overwritten value in the underlying store
    //    and an updated value in the tracking store

    [Theory, CombinatorialData]
    public void tryget_value_for_valid_key(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        var store = GetStore(storeType);
        test_tryget_value_for_valid_key(store, index);
    }

    [Theory, CombinatorialData]
    public void contains_false_for_missing_key(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_contains_false_for_missing_key(store);
    }

    [Theory, CombinatorialData]
    public void tryget_null_for_missing_value(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_tryget_null_for_missing_value(store);
    }

    [Theory, CombinatorialData]
    public void contains_true_for_valid_key(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        var store = GetStore(storeType);
        test_contains_true_for_valid_key(store, index);
    }

    [Theory, CombinatorialData]
    public void can_seek_forward_no_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_can_seek_forward_no_prefix(store);
    }

    [Theory, CombinatorialData]
    public void can_seek_backwards_no_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_can_seek_backwards_no_prefix(store);
    }

    [Theory, CombinatorialData]
    public void seek_forwards_with_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_seek_forwards_with_prefix(store);
    }

    [Theory, CombinatorialData]
    public void seek_backwards_with_prefix(StoreType storeType)
    {
        var store = GetStore(storeType);
        test_seek_backwards_with_prefix(store);
    }

    [Theory, CombinatorialData]
    public void put_new_value(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_put_new_value(store);
    }

    [Theory, CombinatorialData]
    public void put_overwrite_existing_value(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetStore(storeType);
        test_put_overwrite_existing_value(store, index);
    }

    [Theory, CombinatorialData]
    public void tryget_return_null_for_deleted_key(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetStore(storeType);
        test_tryget_return_null_for_deleted_key(store, index);
    }

    [Theory, CombinatorialData]
    public void contains_false_for_deleted_key(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetStore(storeType);
        test_contains_false_for_deleted_key(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_add(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_add(store);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_update(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_update(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_commit_delete(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetStore(storeType);
        test_snapshot_commit_delete(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_addition(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_addition(store);
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_update(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_update(store, index);
    }

    [Theory, CombinatorialData]
    public void snapshot_isolation_delete(StoreType storeType, [CombinatorialValues(0, 1, 5)] int index)
    {
        using var store = GetStore(storeType);
        test_snapshot_isolation_delete(store, index);
    }

    [Theory, CombinatorialData]
    public void key_instance_isolation(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_key_instance_isolation(store);
    }

    [Theory, CombinatorialData]
    public void value_instance_isolation(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_value_instance_isolation(store);
    }

    [Theory, CombinatorialData]
    public void put_null_value_throws(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_put_null_value_throws(store);
    }

    [Theory, CombinatorialData]
    public void snapshot_put_null_value_throws(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_snapshot_put_null_value_throws(store);
    }

    [Theory, CombinatorialData]
    public void delete_missing_value_no_effect(StoreType storeType)
    {
        using var store = GetStore(storeType);
        test_delete_missing_value_no_effect(store);
    }

    internal IStore GetStore(StoreType type)
    {
        var memoryStore = new MemoryStore();
        var trackingStore = GetTrackingStore(type, memoryStore);
        var array = TestData.ToArray();
        var overwritten = Bytes("overwritten");

        for (var i = 0; i < array.Length; i++)
        {
            // put value to be overwritten to underlying store for odd, factor of five indexes
            if (i % 2 == 1 && i % 5 == 0)
                memoryStore.Put(array[i].key, overwritten);

            // put value to underlying store for even indexes, tracking store for odd indexes
            IStore store = i % 2 == 0 ? memoryStore : trackingStore;
            store.Put(array[i].key, array[i].value);
        }

        return trackingStore;
    }

    IStore GetTrackingStore(StoreType storeType, IReadOnlyStore store)
        => storeType switch
        {
            StoreType.MemoryTracking => new MemoryTrackingStore(store),
            StoreType.PersistentTracking =>
                new PersistentTrackingStore(RocksDbUtility.OpenDb(path), store),
            _ => throw new ArgumentException("Unknown StoreType", nameof(storeType)),
        };

    class DisposableStore : IReadOnlyStore, IDisposable
    {
        public bool Disposed { get; private set; } = false;

        public void Dispose()
        {
            Disposed = true;
        }

        byte[]? IReadOnlyStore.TryGet(byte[]? key) => null;
        bool IReadOnlyStore.Contains(byte[]? key) => false;
        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Seek(byte[]? key, SeekDirection direction)
            => Enumerable.Empty<(byte[], byte[])>();
    }
}
