using Neo;
using Neo.IO;
using Neo.Trie;
using Neo.Trie.MPT;
using OneOf;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class CheckpointStore
    {
        class DataTracker<TKey, TValue>
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            private readonly RocksDb db;
            private readonly ColumnFamilyHandle columnFamily;
            private UInt256? root;

            // dictionary value of None indicates the key has been deleted
            private ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> updatedValues =
                ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>>.Empty
                    .WithComparers(new ByteArrayComparer());

            public DataTracker(RocksDb db, string familyName)
            {
                this.db = db;
                columnFamily = db.GetColumnFamily(familyName);
            }

            public Neo.IO.Caching.DataCache<TKey, TValue> GetCache()
                => new DataCache<TKey, TValue>(this);

            public Neo.IO.Caching.DataCache<TKey, TValue> GetSnapshot(IKVStore? kvStore = null)
                => kvStore == null
                    ? new DataCache<TKey, TValue>(this, updatedValues, null)
                    : new DataCache<TKey, TValue>(this, updatedValues, new MPTTrie(GetRoot(), kvStore));

            public IEnumerable<KeyValuePair<TKey, TValue>> Find(byte[] keyPrefix, ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>>? snapshotValues)
            {
                static bool PrefixEquals(byte[] prefix, byte[] value)
                    => prefix.Length == 0
                        || prefix.AsSpan().SequenceEqual(value.AsSpan().Slice(0, prefix.Length));

                snapshotValues ??= updatedValues;
                foreach (var kvp in snapshotValues)
                {
                    if (PrefixEquals(keyPrefix, kvp.Key) && kvp.Value.IsT0)
                    {
                        yield return new KeyValuePair<TKey, TValue>(
                            kvp.Key.AsSerializable<TKey>(),
                            kvp.Value.AsT0);
                    }
                }

                foreach (var kvp in db.Find<TValue>(keyPrefix, columnFamily))
                {
                    if (!snapshotValues.ContainsKey(kvp.Key))
                    {
                        yield return new KeyValuePair<TKey, TValue>(
                            kvp.Key.AsSerializable<TKey>(),
                            kvp.Value);
                    }
                }
            }

            public TValue? TryGet(TKey key, ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>>? snapshotValues)
            {
                snapshotValues ??= updatedValues;
                var keyArray = key.ToArray();

                if (snapshotValues.TryGetValue(keyArray, out var value))
                {
                    return value.Match<TValue?>(v => v, _ => null);
                }

                return db.TryGet<TValue>(keyArray, columnFamily);
            }

            public void Update(byte[] key, OneOf<TValue, OneOf.Types.None> value)
                => updatedValues = updatedValues.SetItem(key, value);

            private UInt256? GetRoot()
            {
                if (root != null)
                {
                    return root;
                }

                return RocksDbStore.GetRoot(db);
            }

            public void PutRoot(UInt256 root)
            {
                this.root = root;
            }
        }
    }
}
