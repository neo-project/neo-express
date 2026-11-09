// Copyright (C) 2015-2026 The Neo Project.
//
// KeyedAsyncLock.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.BlockchainToolkit.Persistence
{
    // Per-key mutual exclusion usable from both sync and async code. Locks for distinct keys
    // are independent. Entries are reference counted and removed when no longer in use.
    internal sealed class KeyedAsyncLock<TKey> where TKey : notnull
    {
        sealed class Entry
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public int RefCount;
        }

        sealed class Releaser : IDisposable
        {
            readonly KeyedAsyncLock<TKey> owner;
            readonly TKey key;
            readonly Entry entry;
            int released;

            public Releaser(KeyedAsyncLock<TKey> owner, TKey key, Entry entry)
            {
                this.owner = owner;
                this.key = key;
                this.entry = entry;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref released, 1) == 0)
                {
                    entry.Semaphore.Release();
                    owner.Return(key, entry);
                }
            }
        }

        readonly Dictionary<TKey, Entry> entries = new();

        Entry Rent(TKey key)
        {
            lock (entries)
            {
                if (!entries.TryGetValue(key, out var entry))
                {
                    entry = new Entry();
                    entries.Add(key, entry);
                }
                entry.RefCount++;
                return entry;
            }
        }

        void Return(TKey key, Entry entry)
        {
            lock (entries)
            {
                if (--entry.RefCount == 0)
                    entries.Remove(key);
            }
        }

        public IDisposable Acquire(TKey key, CancellationToken token)
        {
            var entry = Rent(key);
            try
            {
                entry.Semaphore.Wait(token);
            }
            catch
            {
                Return(key, entry);
                throw;
            }
            return new Releaser(this, key, entry);
        }

        public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken token)
        {
            var entry = Rent(key);
            try
            {
                await entry.Semaphore.WaitAsync(token).ConfigureAwait(false);
            }
            catch
            {
                Return(key, entry);
                throw;
            }
            return new Releaser(this, key, entry);
        }
    }
}
