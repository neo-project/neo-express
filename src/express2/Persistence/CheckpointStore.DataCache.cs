using Neo.IO;
using OneOf;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Neo.Express.Backend2.Persistence
{
    internal partial class CheckpointStore
    {
        private class DataCache<TKey, TValue> : IO.Caching.DataCache<TKey, TValue>
            where TKey : IEquatable<TKey>, IO.ISerializable, new()
            where TValue : class, ICloneable<TValue>, IO.ISerializable, new()
        {
            // dictionary value of None indicates the key has been deleted
            private ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> values;

            private readonly RocksDb db;
            private readonly ColumnFamilyHandle columnFamily;
            private readonly Action<TKey, OneOf<TValue, OneOf.Types.None>> updater;

            public ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> Values => values;
            public Action<TKey, OneOf<TValue, OneOf.Types.None>> Updater => (key, value) => values = values.SetItem(key.ToArray(), value);

            public DataCache(RocksDb db, ColumnFamilyHandle columnFamily)
            {
                this.db = db;
                this.columnFamily = columnFamily;

                values = ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>>
                    .Empty.WithComparers(new ByteArrayComparer());
            }

            public DataCache(RocksDb db, ColumnFamilyHandle columnFamily, ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> values,
                Action<TKey, OneOf<TValue, OneOf.Types.None>> updater)
            {
                this.db = db;
                this.columnFamily = columnFamily;
                this.values = values;
                this.updater = updater;
            }

            private TValue GetHelper(TKey key, bool @throw)
            {
                var keyArray = key.ToArray();
                if (values.TryGetValue(keyArray, out var value))
                {
                    if (value.IsT0)
                    {
                        return value.AsT0;
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(value.IsT1);
                        if (@throw)
                        {
                            throw new Exception("not found");
                        }
                        return null;
                    }
                }

                return db.TryGet<TValue>(keyArray, columnFamily);
            }

            public static bool PrefixEquals(byte[] prefix, byte[] value) => prefix.AsSpan().SequenceEqual(value.AsSpan().Slice(0, prefix.Length));

            protected override IEnumerable<KeyValuePair<TKey, TValue>> FindInternal(byte[] keyPrefix)
            {
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

            protected override TValue GetInternal(TKey key) => GetHelper(key, @throw: true);

            protected override TValue TryGetInternal(TKey key) => GetHelper(key, @throw: false);

            protected override void AddInternal(TKey key, TValue value)
            {
                updater?.Invoke(key, value);
            }

            protected override void UpdateInternal(TKey key, TValue value)
            {
                updater?.Invoke(key, value);
            }

            public override void DeleteInternal(TKey key)
            {
                updater?.Invoke(key, new OneOf.Types.None());
            }
        }
    }
}
