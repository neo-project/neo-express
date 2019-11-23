using Neo.IO;
using OneOf;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NeoExpress.Persistence
{
    internal partial class CheckpointStore
    {
        private class DataCache<TKey, TValue> : Neo.IO.Caching.DataCache<TKey, TValue>
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            private readonly RocksDb db;
            private readonly ColumnFamilyHandle columnFamily;
            // dictionary value of None indicates the key has been deleted
            private ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> values;
            private readonly Action<TKey, OneOf<TValue, OneOf.Types.None>>? updater;

            public DataCache(RocksDb db, string familyName, ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> values)
            {
                this.db = db;
                columnFamily = db.GetColumnFamily(familyName);
                this.values = values;
            }

            public DataCache(RocksDb db, string familyName, ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> values,
                Action<TKey, OneOf<TValue, OneOf.Types.None>> updater)
                : this(db, familyName, values)
            {
                this.updater = updater;
            }

            protected override IEnumerable<KeyValuePair<TKey, TValue>> FindInternal(byte[] keyPrefix)
            {
                static bool PrefixEquals(byte[] prefix, byte[] value) => prefix.AsSpan().SequenceEqual(value.AsSpan().Slice(0, prefix.Length));

                foreach (var kvp in values)
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
                    if (!values.ContainsKey(kvp.Key))
                    {
                        yield return new KeyValuePair<TKey, TValue>(
                            kvp.Key.AsSerializable<TKey>(),
                            kvp.Value);
                    }
                }
            }

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member.
            protected override TValue? TryGetInternal(TKey key)
#pragma warning restore CS8609 // Nullability of reference types in return type doesn't match overridden member.
            {
                var keyArray = key.ToArray();
                if (values.TryGetValue(keyArray, out var value))
                {
                    return value.Match<TValue?>(v => v, _ => null);
                }

                return db.TryGet<TValue>(keyArray, columnFamily);
            }

            protected override TValue GetInternal(TKey key)
            {
                var value = TryGetInternal(key);
                if (value == null)
                {
                    throw new Exception("not found");
                }
                else
                {
                    return value;
                }
            }

            protected override void AddInternal(TKey key, TValue value)
            {
                UpdateInternal(key, value);
            }

            protected override void UpdateInternal(TKey key, TValue value)
            {
                if (updater == null)
                    throw new InvalidOperationException();

                values = values.SetItem(key.ToArray(), value);
                updater(key, value);
            }

            public override void DeleteInternal(TKey key)
            {
                if (updater == null)
                    throw new InvalidOperationException();

                values = values.SetItem(key.ToArray(), noneInstance);
                updater(key, noneInstance);
            }
        }
    }
}
