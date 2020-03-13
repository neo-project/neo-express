﻿using Neo.IO;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace NeoExpress.Neo2.Persistence
{
    partial class RocksDbStore
    {
        private class DataCache<TKey, TValue> : Neo.IO.Caching.DataCache<TKey, TValue>
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            private readonly RocksDb db;
            private readonly ColumnFamilyHandle familyHandle;
            private readonly ReadOptions? readOptions;
            private readonly WriteBatch? writeBatch;

            public DataCache(RocksDb db, string familyName, ReadOptions? readOptions = null, WriteBatch? writeBatch = null)
                : this(db, db.GetColumnFamily(familyName), readOptions, writeBatch)
            {
            }

            public DataCache(RocksDb db, ColumnFamilyHandle familyHandle, ReadOptions? readOptions = null, WriteBatch? writeBatch = null)
            {
                this.db = db;
                this.familyHandle = familyHandle;
                this.readOptions = readOptions;
                this.writeBatch = writeBatch;
            }

            protected override IEnumerable<KeyValuePair<TKey, TValue>> FindInternal(byte[] key_prefix)
            {
                return db.Find<TKey, TValue>(key_prefix, familyHandle, readOptions);
            }

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member.
            // Neo 2.x is not compiled with C# 8, so not sure why C# compiler thinks
            // TryGetInternal can't return null. But it can so suppress the warning.
            protected override TValue? TryGetInternal(TKey key)
#pragma warning restore CS8609 // Nullability of reference types in return type doesn't match overridden member.
            {
                return db.TryGet<TKey, TValue>(key, familyHandle, readOptions);
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
                if (writeBatch == null)
                    throw new InvalidOperationException();

                writeBatch.Put(key.ToArray(), value.ToArray(), familyHandle);
            }

            public override void DeleteInternal(TKey key)
            {
                if (writeBatch == null)
                    throw new InvalidOperationException();

                writeBatch.Delete(key.ToArray(), familyHandle);
            }
        }
    }
}
