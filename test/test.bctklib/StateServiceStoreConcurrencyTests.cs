// Copyright (C) 2015-2026 The Neo Project.
//
// StateServiceStoreConcurrencyTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Json;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace test.bctklib
{
    public class StateServiceStoreConcurrencyTests : IDisposable
    {
        static readonly UInt160 CONTRACT_X = UInt160.Parse("0x0000000000000000000000000000000000000011");
        static readonly UInt160 CONTRACT_Y = UInt160.Parse("0x0000000000000000000000000000000000000022");
        static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(30);

        const string EmptyResult = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"truncated\":false,\"results\":[]}}";

        static readonly BranchInfo branchInfo = new(
            1, 53, 0, UInt256.Zero, UInt256.Zero,
            new[] { new ContractInfo(1, CONTRACT_X, "x"), new ContractInfo(2, CONTRACT_Y, "y") });

        readonly string dbPath = Path.Combine(Path.GetTempPath(), "ssscc-" + Guid.NewGuid().ToString("N"));
        RocksDb? db;

        public void Dispose()
        {
            db?.Dispose();
            if (Directory.Exists(dbPath))
                Directory.Delete(dbPath, true);
        }

        sealed class ScriptedHandler : HttpMessageHandler
        {
            readonly Func<int, Task> script;
            readonly Func<int, string> response;
            int count;
            public int RequestCount => Volatile.Read(ref count);

            public ScriptedHandler(Func<int, Task> script, Func<int, string>? response = null)
            {
                this.script = script;
                this.response = response ?? (_ => EmptyResult);
            }

            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
                => Respond().GetAwaiter().GetResult();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Respond();

            async Task<HttpResponseMessage> Respond()
            {
                var n = Interlocked.Increment(ref count);
                await script(n).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response(n), Encoding.UTF8, "application/json")
                };
            }
        }

        static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        ICacheClient CreateCache(string kind)
        {
            if (kind == "memory")
                return new StateServiceStore.MemoryCacheClient();
            db ??= RocksDbUtility.OpenDb(dbPath);
            return new StateServiceStore.RocksDbCacheClient(db, shared: true, "test");
        }

        static StateServiceStore CreateStore(ICacheClient cache, ScriptedHandler handler, BranchInfo? info = null)
        {
            var rpc = new RpcClient(new HttpClient(handler), new Uri("http://localhost:1"));
            return new StateServiceStore(rpc, cache, info ?? branchInfo);
        }

        sealed class ObservedCache : ICacheClient
        {
            readonly ICacheClient inner;
            int attempts;
            public TaskCompletionSource FirstAttempt { get; } = NewGate();
            public TaskCompletionSource SecondAttempt { get; } = NewGate();
            public ObservedCache(ICacheClient inner) => this.inner = inner;
            public object DownloadLockScope => inner.DownloadLockScope;
            public void Dispose() => inner.Dispose();
            public bool TryGetCachedStorage(UInt160 hash, ReadOnlyMemory<byte> key, out byte[]? value)
                => inner.TryGetCachedStorage(hash, key, out value);
            public void CacheStorage(UInt160 hash, ReadOnlyMemory<byte> key, byte[]? value)
                => inner.CacheStorage(hash, key, value);
            public bool IsFoundStatesCached(UInt160 hash, byte? prefix) => inner.IsFoundStatesCached(hash, prefix);
            public bool TryGetCachedFoundStates(UInt160 hash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value)
                => inner.TryGetCachedFoundStates(hash, prefix, out value);
            public ICacheSnapshot GetFoundStatesSnapshot(UInt160 hash, byte? prefix)
                => inner.GetFoundStatesSnapshot(hash, prefix);
            void Observe()
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                    FirstAttempt.SetResult();
                if (attempt == 2)
                    SecondAttempt.SetResult();
            }
            public IDisposable AcquireDownloadLock(UInt160 hash, byte? prefix, CancellationToken token)
            {
                Observe();
                return inner.AcquireDownloadLock(hash, prefix, token);
            }
            public Task<IDisposable> AcquireDownloadLockAsync(UInt160 hash, byte? prefix, CancellationToken token)
            {
                Observe();
                return inner.AcquireDownloadLockAsync(hash, prefix, token);
            }
        }

        static (BranchInfo branch, string complete, string first, string last) ProvenPages()
        {
            using var storage = new MemoryStore();
            using var snapshot = storage.GetSnapshot();
            var trie = new Neo.Cryptography.MPTTrie.Trie(snapshot, null);
            var keys = new[] { new byte[] { 1 }, new byte[] { 2 } };
            var values = new[] { new byte[] { 11 }, new byte[] { 22 } };
            var fullKeys = keys.Select(key => new StorageKey { Id = 1, Key = key }.ToArray()).ToArray();
            for (var i = 0; i < keys.Length; i++)
                trie.Put(fullKeys[i], values[i]);
            trie.Commit();
            snapshot.Commit();
            string Page(bool truncated, params int[] indices)
            {
                var results = new JArray();
                foreach (var i in indices)
                    results.Add(new JObject
                    {
                        ["key"] = Convert.ToBase64String(keys[i]),
                        ["value"] = Convert.ToBase64String(values[i])
                    });
                return new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = 1,
                    ["result"] = new JObject
                    {
                        ["truncated"] = truncated,
                        ["results"] = results,
                        ["firstProof"] = Convert.ToBase64String(trie.GetSerializedProof(fullKeys[indices[0]])),
                        ["lastProof"] = Convert.ToBase64String(trie.GetSerializedProof(fullKeys[indices[^1]]))
                    }
                }.ToString();
            }
            return (branchInfo with { RootHash = trie.Root.Hash }, Page(false, 0, 1), Page(true, 0), Page(false, 1));
        }

        static void AssertContents(ICacheClient cache)
        {
            cache.TryGetCachedFoundStates(CONTRACT_X, null, out var states).Should().BeTrue();
            var entries = states.OrderBy(entry => entry.key.Span[0]).ToArray();
            entries.Should().HaveCount(2);
            entries[0].key.ToArray().Should().Equal(1);
            entries[0].value.Should().Equal(11);
            entries[1].key.ToArray().Should().Equal(2);
            entries[1].value.Should().Equal(22);
        }

        static byte[] SeekKey(int contractId) => BitConverter.GetBytes(contractId);

        static bool IsCached(ICacheClient cache, UInt160 hash)
            => cache.TryGetCachedFoundStates(hash, null, out _);

        [Fact]
        public void rocksdb_commit_publishes_data_and_completion_atomically()
        {
            db = RocksDbUtility.OpenDb(dbPath);
            using var observer = new StateServiceStore.RocksDbCacheClient(db, true, "test");
            using var writer = new StateServiceStore.RocksDbCacheClient(db, true, "test", batch =>
            {
                IsCached(observer, CONTRACT_X).Should().BeFalse("the batch has not been written yet");
                db.Write(batch);
            });
            using var snapshot = writer.GetFoundStatesSnapshot(CONTRACT_X, null);
            snapshot.Add(new byte[] { 1, 2 }, new byte[] { 3, 4 });
            snapshot.Commit();
            observer.TryGetCachedFoundStates(CONTRACT_X, null, out var states).Should().BeTrue();
            var entry = states.Single();
            entry.key.ToArray().Should().Equal(1, 2);
            entry.value.Should().Equal(3, 4);
        }

        [Fact]
        public void rocksdb_failed_commit_is_not_a_completed_cache_entry()
        {
            db = RocksDbUtility.OpenDb(dbPath);
            using var writer = new StateServiceStore.RocksDbCacheClient(db, true, "test", _ => throw new IOException("write failed"));
            using (var snapshot = writer.GetFoundStatesSnapshot(CONTRACT_X, null))
            {
                snapshot.Add(new byte[] { 1 }, new byte[] { 2 });
                Action commit = snapshot.Commit;
                commit.Should().Throw<IOException>().WithMessage("write failed");
            }
            IsCached(writer, CONTRACT_X).Should().BeFalse();
            using var retry = new StateServiceStore.RocksDbCacheClient(db, true, "test");
            using (var snapshot = retry.GetFoundStatesSnapshot(CONTRACT_X, null))
            {
                snapshot.Add(new byte[] { 3 }, new byte[] { 4 });
                snapshot.Commit();
            }
            retry.TryGetCachedFoundStates(CONTRACT_X, null, out var states).Should().BeTrue();
            states.Single().key.ToArray().Should().Equal(3);
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task callback_reentry_is_rejected_without_waiting_for_its_own_download(string kind)
        {
            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, new ScriptedHandler(_ => Task.CompletedTask));
            using var cancellation = new CancellationTokenSource();
            Task? nested = null;
            try
            {
                await store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken, _ =>
                {
                    nested = store.PrefetchAsync(CONTRACT_X, cancellation.Token);
                    nested.IsCompleted.Should().BeTrue("reentry must fail immediately rather than await itself");
                }).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
                var error = await Record.ExceptionAsync(() => nested!);
                error.Should().BeOfType<InvalidOperationException>();
            }
            finally
            {
                cancellation.Cancel();
                if (nested is not null)
                    await Record.ExceptionAsync(() => nested.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken));
            }
        }

        // B reaches the same-key lock while A is in flight. Once A commits, B must
        // reuse the data instead of issuing the RPC request scripted to fail.
        [Theory]
        [InlineData("memory", false, false)]
        [InlineData("memory", false, true)]
        [InlineData("memory", true, false)]
        [InlineData("memory", true, true)]
        [InlineData("rocksdb", false, false)]
        [InlineData("rocksdb", false, true)]
        [InlineData("rocksdb", true, false)]
        [InlineData("rocksdb", true, true)]
        public async Task concurrent_download_reuses_committed_contents_instead_of_issuing_failing_rpc(string kind, bool ownerSync, bool waiterSync)
        {
            var gateA = NewGate();
            var firstArrived = NewGate();
            var pages = ProvenPages();
            var handler = new ScriptedHandler(async n =>
            {
                if (n == 1)
                {
                    firstArrived.SetResult();
                    await gateA.Task;
                }
                else
                {
                    throw new HttpRequestException("boom");
                }
            }, _ => pages.complete);

            using var cache = new ObservedCache(CreateCache(kind));
            using var store = CreateStore(cache, handler, pages.branch);

            var a = Start(store, ownerSync);
            await firstArrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            var b = Start(store, waiterSync);
            try
            {
                await cache.SecondAttempt.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            }
            finally
            {
                gateA.TrySetResult();
            }
            await a.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            await b.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            handler.RequestCount.Should().Be(1);
            AssertContents(cache);
        }

        static Task Start(StateServiceStore store, bool sync)
            => sync
                ? Task.Run(() => { _ = store.Seek(SeekKey(1), SeekDirection.Forward); }, TestContext.Current.CancellationToken)
                : store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);

        [Theory]
        [InlineData("memory", false)]
        [InlineData("memory", true)]
        [InlineData("rocksdb", false)]
        [InlineData("rocksdb", true)]
        public async Task failure_after_first_page_leaves_no_partial_data_and_waiter_can_retry(string kind, bool sync)
        {
            var pages = ProvenPages();
            var secondPage = NewGate();
            var fail = NewGate();
            var handler = new ScriptedHandler(async n =>
            {
                if (n == 2)
                {
                    secondPage.SetResult();
                    await fail.Task;
                    throw new HttpRequestException("second page failed");
                }
            }, n => n == 1 ? pages.first : pages.complete);
            using var cache = new ObservedCache(CreateCache(kind));
            using var store = CreateStore(cache, handler, pages.branch);
            var owner = Start(store, sync);
            await secondPage.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            IsCached(cache, CONTRACT_X).Should().BeFalse();
            var waiter = Start(store, sync);
            try
            {
                await cache.SecondAttempt.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            }
            finally
            {
                fail.TrySetResult();
            }
            var error = await Record.ExceptionAsync(() => owner.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken));
            error.Should().BeOfType<HttpRequestException>().Which.Message.Should().Be("second page failed");
            await waiter.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            handler.RequestCount.Should().Be(3);
            AssertContents(cache);
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task cancellation_after_first_page_leaves_no_partial_data_and_allows_retry(string kind)
        {
            var pages = ProvenPages();
            var secondPage = NewGate();
            var release = NewGate();
            var handler = new ScriptedHandler(async n =>
            {
                if (n == 2)
                {
                    secondPage.SetResult();
                    await release.Task;
                }
            }, n => n == 1 ? pages.first : n == 2 ? pages.last : pages.complete);
            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, handler, pages.branch);
            using var cancellation = new CancellationTokenSource();
            var task = store.PrefetchAsync(CONTRACT_X, cancellation.Token);
            await secondPage.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            IsCached(cache, CONTRACT_X).Should().BeFalse();
            cancellation.Cancel();
            release.SetResult();
            var error = await Record.ExceptionAsync(() => task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken));
            error.Should().BeAssignableTo<OperationCanceledException>();
            IsCached(cache, CONTRACT_X).Should().BeFalse();
            await store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            AssertContents(cache);
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task cancellation_from_callback_prevents_commit(string kind)
        {
            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, new ScriptedHandler(_ => Task.CompletedTask));
            using var cancellation = new CancellationTokenSource();
            var error = await Record.ExceptionAsync(() => store.PrefetchAsync(CONTRACT_X, cancellation.Token, _ => cancellation.Cancel()));
            error.Should().BeAssignableTo<OperationCanceledException>();
            IsCached(cache, CONTRACT_X).Should().BeFalse();
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task synchronous_seek_reentry_from_callback_fails_without_deadlocking(string kind)
        {
            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, new ScriptedHandler(_ => Task.CompletedTask));
            await store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken, _ =>
            {
                Action seek = () => store.Seek(SeekKey(1), SeekDirection.Forward);
                seek.Should().Throw<InvalidOperationException>().WithMessage("*callback*reenter*");
            }).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            IsCached(cache, CONTRACT_X).Should().BeTrue();
        }

        [Theory]
        [InlineData("memory", false)]
        [InlineData("memory", true)]
        [InlineData("rocksdb", false)]
        [InlineData("rocksdb", true)]
        public async Task disposal_rejects_new_operations_but_allows_active_and_waiting_downloads_to_finish(string kind, bool sync)
        {
            var arrived = NewGate();
            var release = NewGate();
            var handler = new ScriptedHandler(async _ => { arrived.SetResult(); await release.Task; });
            using var cache = new ObservedCache(CreateCache(kind));
            using var store = CreateStore(cache, handler);
            var owner = Start(store, sync);
            await arrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            var waiter = Start(store, sync);
            await cache.SecondAttempt.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            store.Dispose();
            try
            {
                var error = await Record.ExceptionAsync(() => store.PrefetchAsync(CONTRACT_Y, TestContext.Current.CancellationToken));
                error.Should().BeOfType<ObjectDisposedException>();
            }
            finally { release.TrySetResult(); }
            await Task.WhenAll(owner, waiter).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            Action read = () => IsCached(cache, CONTRACT_X);
            read.Should().Throw<ObjectDisposedException>();
            handler.RequestCount.Should().Be(1);
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task dispose_inside_callback_does_not_deadlock(string kind)
        {
            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, new ScriptedHandler(_ => Task.CompletedTask));
            await store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken, _ => store.Dispose()).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            Action read = () => IsCached(cache, CONTRACT_X);
            read.Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void disposing_owner_rejects_snapshots_and_other_clients_before_native_access()
        {
            db = RocksDbUtility.OpenDb(dbPath);
            using var owner = new StateServiceStore.RocksDbCacheClient(db, false, "test");
            using var shared = new StateServiceStore.RocksDbCacheClient(db, true, "test");
            using var snapshot = shared.GetFoundStatesSnapshot(CONTRACT_X, null);
            snapshot.Add(new byte[] { 1 }, new byte[] { 2 });
            owner.Dispose();
            Action commit = snapshot.Commit;
            commit.Should().Throw<ObjectDisposedException>();
            Action read = () => IsCached(shared, CONTRACT_X);
            read.Should().Throw<ObjectDisposedException>();
            db = null;
        }

        [Fact]
        public void rocksdb_ignores_legacy_families_and_preserves_binary_keys_across_reopen()
        {
            db = RocksDbUtility.OpenDb(dbPath);
            var options = new ColumnFamilyOptions();
            var legacy = db.CreateColumnFamily(options, $"test.{CONTRACT_X}");
            db.Put(new byte[] { 99 }, new byte[] { 99 }, legacy);
            var keys = new[] { Array.Empty<byte>(), new byte[] { 0 }, new byte[] { 1 }, new byte[] { 255 } };
            using (var cache = CreateCache("rocksdb"))
            {
                IsCached(cache, CONTRACT_X).Should().BeFalse();
                using var snapshot = cache.GetFoundStatesSnapshot(CONTRACT_X, null);
                foreach (var key in keys)
                    snapshot.Add(key, new byte[] { 42 });
                snapshot.Commit();
            }
            db.Dispose();
            db = RocksDbUtility.OpenDb(dbPath);
            using var reopened = CreateCache("rocksdb");
            reopened.TryGetCachedFoundStates(CONTRACT_X, null, out var states).Should().BeTrue();
            var entries = states.ToArray();
            entries.Select(entry => Convert.ToHexString(entry.key.Span)).Should().Equal(keys.Select(Convert.ToHexString));
            entries.Should().OnlyContain(entry => entry.value.Length == 1 && entry.value[0] == 42);
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task cancelled_prefetch_does_not_drop_entry_committed_by_concurrent_download(string kind)
        {
            var gateA = NewGate();
            var gateB = NewGate();
            var firstArrived = NewGate();
            var handler = new ScriptedHandler(async n =>
            {
                if (n == 1)
                {
                    firstArrived.SetResult();
                    await gateA.Task;
                }
                else
                {
                    await gateB.Task;
                }
            });

            using var cache = new ObservedCache(CreateCache(kind));
            using var store = CreateStore(cache, handler);
            using var cts = new CancellationTokenSource();

            var a = store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);
            await firstArrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            var b = store.PrefetchAsync(CONTRACT_X, cts.Token);
            await cache.SecondAttempt.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            cts.Cancel();

            var ex = await Record.ExceptionAsync(() => b.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken));
            ex.Should().BeAssignableTo<OperationCanceledException>("cancellation must interrupt waiting for A");

            gateA.SetResult();
            await a.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            gateB.SetResult();

            handler.RequestCount.Should().Be(1);
            IsCached(cache, CONTRACT_X).Should().BeTrue();
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task incomplete_download_is_not_visible_as_cached_entry(string kind)
        {
            var gate = NewGate();
            var arrived = NewGate();
            var handler = new ScriptedHandler(async n =>
            {
                arrived.SetResult();
                await gate.Task;
                throw new HttpRequestException("boom");
            });

            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, handler);

            var a = store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);
            await arrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            IsCached(cache, CONTRACT_X).Should().BeFalse("in-flight download is not complete");

            gate.SetResult();
            var ex = await Record.ExceptionAsync(() => a.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken));
            ex.Should().BeOfType<HttpRequestException>().Which.Message.Should().Be("boom");
            IsCached(cache, CONTRACT_X).Should().BeFalse("failed download leaves nothing behind");
        }

        // Concurrent prefetches of the same key should hit the state service only once.
        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task concurrent_prefetch_of_same_key_downloads_once(string kind)
        {
            var gate = NewGate();
            var arrived = NewGate();
            var handler = new ScriptedHandler(async n =>
            {
                arrived.TrySetResult();
                await gate.Task;
            });

            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, handler);

            var a = store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);
            await arrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            var b = store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);

            gate.SetResult();
            await Task.WhenAll(a, b).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);

            handler.RequestCount.Should().Be(1);
            IsCached(cache, CONTRACT_X).Should().BeTrue();
        }

        // Two store instances sharing one RocksDB: the second must neither re-download
        // nor clobber the first one's data.
        [Fact]
        public async Task stores_sharing_a_rocksdb_do_not_duplicate_or_drop_downloads()
        {
            var gate = NewGate();
            var arrived = NewGate();
            var pages = ProvenPages();
            var handler1 = new ScriptedHandler(async n => { arrived.SetResult(); await gate.Task; }, _ => pages.complete);
            var handler2 = new ScriptedHandler(n => throw new HttpRequestException("boom"));

            using var cache1 = CreateCache("rocksdb");
            using var cache2 = new ObservedCache(CreateCache("rocksdb"));
            using var store1 = CreateStore(cache1, handler1, pages.branch);
            using var store2 = CreateStore(cache2, handler2, pages.branch);

            var a = store1.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);
            await arrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            var b = store2.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);

            try
            {
                await cache2.FirstAttempt.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            }
            finally
            {
                gate.TrySetResult();
            }
            await a.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            await b.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);

            AssertContents(cache1);
            AssertContents(cache2);
            handler2.RequestCount.Should().Be(0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task callback_reentry_into_another_store_sharing_the_cache_is_rejected(bool sync)
        {
            using var cache1 = CreateCache("rocksdb");
            using var cache2 = CreateCache("rocksdb");
            var handler2 = new ScriptedHandler(_ => throw new InvalidOperationException("unexpected RPC"));
            using var store1 = CreateStore(cache1, new ScriptedHandler(_ => Task.CompletedTask));
            using var store2 = CreateStore(cache2, handler2);
            await store1.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken, _ =>
            {
                Action reenter = () =>
                {
                    if (sync)
                        store2.Seek(SeekKey(1), SeekDirection.Forward);
                    else
                        store2.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                };
                reenter.Should().Throw<InvalidOperationException>().WithMessage("*callback*reenter*");
            }).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            handler2.RequestCount.Should().Be(0);
            IsCached(cache2, CONTRACT_X).Should().BeTrue();
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task concurrent_callbacks_cannot_deadlock_across_different_keys(string kind)
        {
            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, new ScriptedHandler(_ => Task.CompletedTask));
            using var bothInCallback = new Barrier(2);
            var nestedErrors = new System.Collections.Concurrent.ConcurrentBag<Exception?>();

            Task Run(UInt160 own, UInt160 other)
            {
                var invoked = 0;
                return Task.Run(() => store.PrefetchAsync(own, TestContext.Current.CancellationToken, _ =>
                {
                    // The callback runs once per page; only the first invocation takes part in the rendezvous.
                    if (Interlocked.Exchange(ref invoked, 1) == 1)
                        return;
                    bothInCallback.SignalAndWait(SafetyTimeout).Should().BeTrue();
                    nestedErrors.Add(Record.Exception(() => store.PrefetchAsync(other, TestContext.Current.CancellationToken).GetAwaiter().GetResult()));
                    // Neither download may commit before both nested attempts ran; otherwise the
                    // other key is already cached and the nested call returns without taking a lock.
                    bothInCallback.SignalAndWait(SafetyTimeout).Should().BeTrue();
                }), TestContext.Current.CancellationToken);
            }

            await Task.WhenAll(Run(CONTRACT_X, CONTRACT_Y), Run(CONTRACT_Y, CONTRACT_X))
                .WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            nestedErrors.Should().HaveCount(2).And.AllBeOfType<InvalidOperationException>();
            IsCached(cache, CONTRACT_X).Should().BeTrue();
            IsCached(cache, CONTRACT_Y).Should().BeTrue();
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task dispose_during_active_seek_does_not_fail_the_seek(string kind)
        {
            var arrived = NewGate();
            var release = NewGate();
            var handler = new ScriptedHandler(async _ => { arrived.TrySetResult(); await release.Task; });
            using var cache = CreateCache(kind);
            var store = CreateStore(cache, handler);
            var seek = Task.Run(() => store.Seek(SeekKey(1), SeekDirection.Forward).ToList(), TestContext.Current.CancellationToken);
            await arrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            store.Dispose();
            release.SetResult();
            var error = await Record.ExceptionAsync(() => seek.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken));
            error.Should().BeNull();
        }

        [Fact]
        public async Task callback_cannot_download_in_an_independent_cache_namespace()
        {
            db = RocksDbUtility.OpenDb(dbPath);
            using var cache1 = new StateServiceStore.RocksDbCacheClient(db, true, "first");
            using var cache2 = new StateServiceStore.RocksDbCacheClient(db, true, "second");
            var handler2 = new ScriptedHandler(_ => throw new InvalidOperationException("unexpected RPC"));
            using var store1 = CreateStore(cache1, new ScriptedHandler(_ => Task.CompletedTask));
            using var store2 = CreateStore(cache2, handler2);
            await store1.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken, _ =>
            {
                Action nested = () => store2.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                nested.Should().Throw<InvalidOperationException>().WithMessage("*callback*reenter*");
            }).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            handler2.RequestCount.Should().Be(0);
            IsCached(cache2, CONTRACT_X).Should().BeFalse();
        }

        [Fact]
        public async Task concurrent_callbacks_cannot_deadlock_across_independent_stores()
        {
            db = RocksDbUtility.OpenDb(dbPath);
            using var cache1 = new StateServiceStore.RocksDbCacheClient(db, true, "first");
            using var cache2 = new StateServiceStore.RocksDbCacheClient(db, true, "second");
            using var store1 = CreateStore(cache1, new ScriptedHandler(_ => Task.CompletedTask));
            using var store2 = CreateStore(cache2, new ScriptedHandler(_ => Task.CompletedTask));
            using var bothInCallback = new Barrier(2);
            var nestedErrors = new System.Collections.Concurrent.ConcurrentBag<Exception?>();

            Task Run(StateServiceStore own, StateServiceStore other)
            {
                var invoked = 0;
                return Task.Run(() => own.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken, _ =>
                {
                    if (Interlocked.Exchange(ref invoked, 1) == 1)
                        return;
                    bothInCallback.SignalAndWait(SafetyTimeout).Should().BeTrue();
                    nestedErrors.Add(Record.Exception(() => other.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken).GetAwaiter().GetResult()));
                    bothInCallback.SignalAndWait(SafetyTimeout).Should().BeTrue();
                }), TestContext.Current.CancellationToken);
            }

            await Task.WhenAll(Run(store1, store2), Run(store2, store1))
                .WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            nestedErrors.Should().HaveCount(2).And.AllBeOfType<InvalidOperationException>();
            IsCached(cache1, CONTRACT_X).Should().BeTrue();
            IsCached(cache2, CONTRACT_X).Should().BeTrue();
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public void discarding_an_uncommitted_snapshot_preserves_another_snapshots_committed_data(string kind)
        {
            using var cache = CreateCache(kind);
            using var winner = cache.GetFoundStatesSnapshot(CONTRACT_X, null);
            var loser = cache.GetFoundStatesSnapshot(CONTRACT_X, null);
            loser.Add(new byte[] { 99 }, new byte[] { 99 });
            winner.Add(new byte[] { 1 }, new byte[] { 11 });
            winner.Commit();
            loser.Dispose();
            cache.TryGetCachedFoundStates(CONTRACT_X, null, out var states).Should().BeTrue();
            var entry = states.Single();
            entry.key.ToArray().Should().Equal(1);
            entry.value.Should().Equal(11);
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task callback_exception_propagates_without_publishing_data_and_releases_the_lock(string kind)
        {
            var pages = ProvenPages();
            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, new ScriptedHandler(_ => Task.CompletedTask, _ => pages.complete), pages.branch);
            var expected = new InvalidOperationException("callback failed");
            var error = await Record.ExceptionAsync(() => store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken, _ => throw expected));
            error.Should().BeSameAs(expected);
            IsCached(cache, CONTRACT_X).Should().BeFalse();
            await store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
            AssertContents(cache);
        }

        [Theory]
        [InlineData("memory")]
        [InlineData("rocksdb")]
        public async Task locks_for_different_prefixes_are_independent_and_same_key_waits(string kind)
        {
            using var cache = CreateCache(kind);
            using var timeout = new CancellationTokenSource(SafetyTimeout);
            using var first = cache.AcquireDownloadLock(CONTRACT_X, 1, timeout.Token);
            using var otherPrefix = await cache.AcquireDownloadLockAsync(CONTRACT_X, 2, timeout.Token);
            using var wholeContract = await cache.AcquireDownloadLockAsync(CONTRACT_X, null, timeout.Token);
            var sameKey = cache.AcquireDownloadLockAsync(CONTRACT_X, 1, timeout.Token);
            sameKey.IsCompleted.Should().BeFalse();
            first.Dispose();
            using var acquired = await sameKey.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        }

        [Fact]
        public void cached_results_can_be_enumerated_after_owned_database_is_closed()
        {
            db = RocksDbUtility.OpenDb(dbPath);
            using var owner = new StateServiceStore.RocksDbCacheClient(db, false, "test");
            using (var snapshot = owner.GetFoundStatesSnapshot(CONTRACT_X, null))
            {
                snapshot.Add(new byte[] { 1 }, new byte[] { 11 });
                snapshot.Commit();
            }
            owner.TryGetCachedFoundStates(CONTRACT_X, null, out var states).Should().BeTrue();
            owner.Dispose();
            db = null;
            var entry = states.Single();
            entry.key.ToArray().Should().Equal(1);
            entry.value.Should().Equal(11);
        }

        // A slow download of one contract must not block downloads of another.
        [Theory]
        [InlineData("memory", false)]
        [InlineData("memory", true)]
        [InlineData("rocksdb", false)]
        [InlineData("rocksdb", true)]
        public async Task downloads_of_unrelated_contracts_are_not_serialized(string kind, bool sync)
        {
            var gate = NewGate();
            var arrived = NewGate();
            var handler = new ScriptedHandler(async n =>
            {
                if (n == 1)
                {
                    arrived.SetResult();
                    await gate.Task;
                }
            });

            using var cache = CreateCache(kind);
            using var store = CreateStore(cache, handler);

            var x = store.PrefetchAsync(CONTRACT_X, TestContext.Current.CancellationToken);
            await arrived.Task.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);

            var y = sync
                ? Task.Run(() => { _ = store.Seek(SeekKey(2), SeekDirection.Forward); }, TestContext.Current.CancellationToken)
                : store.PrefetchAsync(CONTRACT_Y, TestContext.Current.CancellationToken);
            await y.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);

            IsCached(cache, CONTRACT_Y).Should().BeTrue();
            IsCached(cache, CONTRACT_X).Should().BeFalse();

            gate.SetResult();
            await x.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        }
    }
}
