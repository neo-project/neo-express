﻿using Neo.IO;
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

            public Neo.IO.Caching.DataCache<TKey, TValue> GetSnapshot()
                => new DataCache<TKey, TValue>(this, updatedValues, Update);

            public IEnumerable<KeyValuePair<TKey, TValue>> Find(byte[] keyPrefix, ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>>? snapshotValues)
            {
                static bool PrefixEquals(byte[] prefix, byte[] value)
                    => prefix.Length == 0
                        ? true
                        : prefix.AsSpan().SequenceEqual(value.AsSpan().Slice(0, prefix.Length));

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

            private void Update(TKey key, OneOf<TValue, OneOf.Types.None> value)
                => updatedValues = updatedValues.SetItem(key.ToArray(), value);
        }
    }
}
