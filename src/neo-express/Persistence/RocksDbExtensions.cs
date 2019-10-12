using Neo.IO;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoExpress.Persistence
{
    internal static class RocksDbExtensions
    {
        public static IEnumerable<KeyValuePair<TKey, TValue>> Find<TKey, TValue>(this RocksDb db, byte[] keyPrefix, ColumnFamilyHandle? columnFamily = null, ReadOptions? readOptions = null)
            where TKey : ISerializable, new()
            where TValue : ISerializable, new()
        {
            return Find<TValue>(db, keyPrefix, columnFamily, readOptions)
                .Select(kvp => new KeyValuePair<TKey, TValue>(kvp.Key.AsSerializable<TKey>(), kvp.Value));
        }

        public static IEnumerable<KeyValuePair<byte[], TValue>> Find<TValue>(this RocksDb db, byte[] keyPrefix, ColumnFamilyHandle? columnFamily = null, ReadOptions? readOptions = null)
            where TValue : ISerializable, new()
        {
            using (var iterator = db.NewIterator(columnFamily, readOptions))
            {
                iterator.Seek(keyPrefix);
                while (iterator.Valid())
                {
                    yield return new KeyValuePair<byte[], TValue>(
                        iterator.Key(), iterator.Value().AsSerializable<TValue>());
                    iterator.Next();
                }
            }
        }

        public static TValue? TryGet<TKey, TValue>(this RocksDb db, TKey key, ColumnFamilyHandle? columnFamily = null, ReadOptions? readOptions = null)
            where TKey : ISerializable
            where TValue : class, ISerializable, new()
        {
            return TryGet<TValue>(db, key.ToArray(), columnFamily, readOptions);
        }

        public static TValue? TryGet<TValue>(this RocksDb db, byte[] key, ColumnFamilyHandle? columnFamily = null, ReadOptions? readOptions = null)
            where TValue : class, ISerializable, new()
        {
            var value = db.Get(key, columnFamily, readOptions);
            return value?.Length > 0
                ? value.AsSerializable<TValue>()
                : null;
        }

        public static TValue Get<TKey, TValue>(this RocksDb db, TKey key, ColumnFamilyHandle? columnFamily = null, ReadOptions? readOptions = null)
            where TKey : ISerializable
            where TValue : class, ISerializable, new()
        {
            return Get<TValue>(db, key.ToArray(), columnFamily, readOptions);
        }

        public static TValue Get<TValue>(this RocksDb db, byte[] key, ColumnFamilyHandle? columnFamily = null, ReadOptions? readOptions = null)
            where TValue : class, ISerializable, new()
        {
            var value = db.Get(key, columnFamily, readOptions);
            if (value?.Length > 0)
            {
                return value.AsSerializable<TValue>();
            }

            throw new Exception("not found");
        }
    }
}
