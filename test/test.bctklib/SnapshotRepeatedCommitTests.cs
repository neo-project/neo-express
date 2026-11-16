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
using Neo.BlockchainToolkit.Utilities;
using Neo.Persistence;
using Neo.Persistence.Providers;
using OneOf;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

    [Fact]
    public async System.Threading.Tasks.Task memory_tracking_concurrent_put_and_commit_never_replays_committed_writes()
    {
        const int keyCount = 2000;
        using var store = new MemoryTrackingStore(new MemoryStore());
        using var a = store.GetSnapshot();

        var token = TestContext.Current.CancellationToken;
        var writer = System.Threading.Tasks.Task.Run(() =>
        {
            for (var i = 0; i < keyCount; i++)
                a.Put(Bytes($"key-{i}"), Bytes("old"));
        }, token);
        var committers = Enumerable.Range(0, 2).Select(_ => System.Threading.Tasks.Task.Run(() =>
        {
            while (!writer.IsCompleted)
                a.Commit();
        }, token)).ToArray();

        await writer;
        await System.Threading.Tasks.Task.WhenAll(committers);
        a.Commit();

        using (var b = store.GetSnapshot())
        {
            for (var i = 0; i < keyCount; i++)
                b.Put(Bytes($"key-{i}"), Bytes("newer"));
            b.Commit();
        }

        a.Commit();

        for (var i = 0; i < keyCount; i++)
        {
            store.TryGet(Bytes($"key-{i}"), out var value).Should().BeTrue();
            value.Should().Equal(Bytes("newer"));
        }
    }

    [Fact]
    public async Task memory_tracking_serializes_pending_batches_in_handoff_order()
    {
        using var backing = new MemoryStore();
        using var store = new MemoryTrackingStore(backing);
        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var calls = 0;
        var map = ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, OneOf.Types.None>>.Empty
            .WithComparers(MemorySequenceComparer.Default);
        Action<ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, OneOf.Types.None>>> commit = batch =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstEntered.Set();
                releaseFirst.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
            }
            else secondEntered.Set();
            foreach (var change in batch)
                store.Put(change.Key.ToArray(), change.Value.AsT0.ToArray());
        };
        using var snapshot = (IStoreSnapshot)Activator.CreateInstance(
            typeof(MemoryTrackingStore).GetNestedType("Snapshot", BindingFlags.NonPublic)!, backing, map, commit, store)!;
        var key = Bytes("key");
        snapshot.Put(key, Bytes("old"));
        var first = Task.Run(snapshot.Commit, TestContext.Current.CancellationToken);
        Task second = Task.CompletedTask;
        var overtook = false;
        try
        {
            firstEntered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
            snapshot.Put(key, Bytes("new"));
            second = Task.Run(() => { secondStarted.Set(); snapshot.Commit(); }, TestContext.Current.CancellationToken);
            secondStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
            overtook = secondEntered.Wait(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseFirst.Set();
            await Task.WhenAll(first, second);
        }
        overtook.Should().BeFalse("a later batch must not commit ahead of an earlier batch");
        store.TryGet(key, out var actual).Should().BeTrue();
        actual.Should().Equal(Bytes("new"));
    }

    [Fact]
    public async Task memory_tracking_same_key_writers_keep_local_and_committed_views_consistent()
    {
        using var store = new MemoryTrackingStore(new MemoryStore());
        using var snapshot = store.GetSnapshot();
        var key = Bytes("key");
        using var start = new Barrier(2);
        var writers = Enumerable.Range(0, 2).Select(writer => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                start.SignalAndWait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
                snapshot.Put(key, Bytes($"{writer}-{i}"));
                start.SignalAndWait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
                if (writer == 0)
                {
                    snapshot.Commit();
                    snapshot.TryGet(key, out var local).Should().BeTrue();
                    store.TryGet(key, out var committed).Should().BeTrue();
                    committed.Should().Equal(local);
                    snapshot.Find().Single().Value.Should().Equal(local);
                }
                start.SignalAndWait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
            }
        }, TestContext.Current.CancellationToken));
        await Task.WhenAll(writers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void memory_tracking_restores_failed_batch_without_overwriting_newer_changes(bool delete)
    {
        using var backing = new MemoryStore();
        using var store = new MemoryTrackingStore(backing);
        var key = Bytes("key");
        var retained = Bytes("retained");
        var calls = 0;
        var empty = ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, OneOf.Types.None>>.Empty
            .WithComparers(MemorySequenceComparer.Default);
        var persisted = empty;
        IStoreSnapshot snapshot = null!;
        Action<ImmutableDictionary<ReadOnlyMemory<byte>, OneOf<ReadOnlyMemory<byte>, OneOf.Types.None>>> commit = batch =>
        {
            if (++calls == 1)
            {
                if (delete) snapshot.Delete(key);
                else snapshot.Put(key, Bytes("new"));
                throw new InvalidOperationException("Commit failed");
            }
            persisted = batch;
        };
        snapshot = (IStoreSnapshot)Activator.CreateInstance(
            typeof(MemoryTrackingStore).GetNestedType("Snapshot", BindingFlags.NonPublic)!, backing, empty, commit, store)!;
        using (snapshot)
        {
            snapshot.Put(key, Bytes("old"));
            snapshot.Put(retained, Bytes("keep"));
            Action firstCommit = snapshot.Commit;
            firstCommit.Should().Throw<InvalidOperationException>();
            snapshot.Commit();
            persisted.Should().HaveCount(2);
            persisted[retained].AsT0.ToArray().Should().Equal(Bytes("keep"));
            persisted[key].IsT1.Should().Be(delete);
            if (!delete) persisted[key].AsT0.ToArray().Should().Equal(Bytes("new"));
            snapshot.Commit();
            persisted.Should().BeEmpty();
        }
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
