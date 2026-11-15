// Copyright (C) 2015-2026 The Neo Project.
//
// SnapshotRepeatedCommitTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System;
using System.Linq;
using Xunit;

namespace test.bctklib;

using static Utility;

public class SnapshotRepeatedCommitTests : IDisposable
{
    public enum StoreType { Memory, RocksDb, MemoryTracking, PersistentTracking }

    readonly CleanupPath path = new CleanupPath();

    public void Dispose()
    {
        path.Dispose();
        GC.SuppressFinalize(this);
    }

    IStore GetStore(StoreType type) => type switch
    {
        StoreType.Memory => new MemoryStore(),
        StoreType.RocksDb => new RocksDbStore(RocksDbUtility.OpenDb(path)),
        StoreType.MemoryTracking => new MemoryTrackingStore(new MemoryStore()),
        StoreType.PersistentTracking => new PersistentTrackingStore(RocksDbUtility.OpenDb(path), new MemoryStore()),
        _ => throw new ArgumentException("Unknown StoreType", nameof(type)),
    };

    [Theory, CombinatorialData]
    public void repeated_commit_does_not_overwrite_newer_value(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var key = Bytes("key");
        var v1 = Bytes("v1");
        var v2 = Bytes("v2");

        using var a = store.GetSnapshot();
        a.Put(key, v1);
        a.Commit();

        using (var b = store.GetSnapshot())
        {
            b.Put(key, v2);
            b.Commit();
        }

        a.Commit();

        store.TryGet(key, out var actual).Should().BeTrue();
        actual.Should().Equal(v2);
    }

    [Theory, CombinatorialData]
    public void repeated_commit_does_not_delete_recreated_key(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var key = Bytes("key");
        store.Put(key, Bytes("initial"));

        using var a = store.GetSnapshot();
        a.Delete(key);
        a.Commit();
        store.Contains(key).Should().BeFalse();

        using (var b = store.GetSnapshot())
        {
            b.Put(key, Bytes("recreated"));
            b.Commit();
        }

        a.Commit();

        store.Contains(key).Should().BeTrue();
        store.TryGet(key, out var actual).Should().BeTrue();
        actual.Should().Equal(Bytes("recreated"));
    }

    [Theory, CombinatorialData]
    public void subsequent_commit_only_applies_new_changes(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var k1 = Bytes("k1");
        var k2 = Bytes("k2");

        using var a = store.GetSnapshot();
        a.Put(k1, Bytes("a1"));
        a.Commit();

        using (var other = store.GetSnapshot())
        {
            other.Put(k1, Bytes("other"));
            other.Commit();
        }

        a.Put(k2, Bytes("a2"));
        a.Commit();

        store.TryGet(k2, out var v2).Should().BeTrue();
        v2.Should().Equal(Bytes("a2"));
        store.TryGet(k1, out var v1).Should().BeTrue();
        v1.Should().Equal(Bytes("other"));
    }

    [Theory, CombinatorialData]
    public void repeated_commit_without_changes(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var k1 = Bytes("k1");
        var k2 = Bytes("k2");
        store.Put(k1, Bytes("initial"));

        using var a = store.GetSnapshot();
        a.Put(k2, Bytes("a2"));
        a.Commit();

        store.Put(k1, Bytes("updated"));
        store.Delete(k2);

        a.Commit();
        a.Commit();

        store.TryGet(k1, out var v1).Should().BeTrue();
        v1.Should().Equal(Bytes("updated"));
        store.Contains(k2).Should().BeFalse();
        store.Find().Select(kvp => kvp.Key).Should().HaveCount(1);
    }

    [Theory, CombinatorialData]
    public void commit_without_any_changes_is_noop(StoreType storeType)
    {
        using var store = GetStore(storeType);
        store.Put(Bytes("k"), Bytes("v"));

        using var a = store.GetSnapshot();
        a.Commit();
        a.Commit();

        store.Find().Should().HaveCount(1);
        store.TryGet(Bytes("k"), out var v).Should().BeTrue();
        v.Should().Equal(Bytes("v"));
    }

    [Theory, CombinatorialData]
    public void snapshot_read_semantics(StoreType storeType)
    {
        using var store = GetStore(storeType);
        var existing = Bytes("a-existing");
        var added = Bytes("b-added");
        store.Put(existing, Bytes("old"));

        using var snapshot = store.GetSnapshot();

        // before commit: Neo Express snapshots expose their own local writes (Neo MemoryStore snapshots do not);
        // the store itself never sees them
        snapshot.Put(added, Bytes("new"));
        snapshot.Delete(existing);

        if (storeType != StoreType.Memory)
        {
            snapshot.Contains(added).Should().BeTrue();
            snapshot.Contains(existing).Should().BeFalse();
            snapshot.TryGet(added, out var addedBefore).Should().BeTrue();
            addedBefore.Should().Equal(Bytes("new"));
            snapshot.Find().Select(kvp => kvp.Key).Should().SatisfyRespectively(k => k.Should().Equal(added));
            snapshot.Find(added, SeekDirection.Backward).Select(kvp => kvp.Key)
                .Should().SatisfyRespectively(k => k.Should().Equal(added));
        }

        store.Contains(added).Should().BeFalse();
        store.Contains(existing).Should().BeTrue();

        snapshot.Commit();

        // after commit: the store has the changes
        store.Contains(added).Should().BeTrue();
        store.Contains(existing).Should().BeFalse();

        // Neo MemoryStore snapshots do not expose their own writes; the Neo Express stores differ,
        // so only the persisted state is asserted for every implementation.
        store.Find().Select(kvp => kvp.Key).Should().SatisfyRespectively(k => k.Should().Equal(added));
    }
}
