using Neo.IO;
using Neo.Trie.MPT;
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
            private readonly MPTTrie? mptTrie = null;

            public DataCache(RocksDb db, string familyName, ReadOptions? readOptions = null, WriteBatch? writeBatch = null, MPTTrie? mptTrie = null)
                : this(db, db.GetColumnFamily(familyName), readOptions, writeBatch, mptTrie)
            {
            }

            public DataCache(RocksDb db, ColumnFamilyHandle familyHandle, ReadOptions? readOptions = null, WriteBatch? writeBatch = null, MPTTrie? mptTrie = null)
            {
                this.db = db;
                this.familyHandle = familyHandle;
                this.readOptions = readOptions;
                this.writeBatch = writeBatch;
                this.mptTrie = mptTrie;
            }

            protected override IEnumerable<KeyValuePair<TKey, TValue>> FindInternal(byte[] key_prefix)
            {
                return db.Find<TKey, TValue>(key_prefix, familyHandle, readOptions);
            }

#pragma warning disable CS8764 // Nullability of reference types in return type doesn't match overridden member.
            // Neo 2.x is not compiled with C# 8, so not sure why C# compiler thinks
            // TryGetInternal can't return null. But it can so suppress the warning.
            protected override TValue? TryGetInternal(TKey key)
#pragma warning restore CS8764 // Nullability of reference types in return type doesn't match overridden member.
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

                var keyArray = key.ToArray();
                var valueArray = value.ToArray();
                writeBatch.Put(keyArray, valueArray, familyHandle);
                mptTrie?.Put(keyArray, valueArray);
            }

            public override void DeleteInternal(TKey key)
            {
                if (writeBatch == null)
                    throw new InvalidOperationException();

                var keyArray = key.ToArray();
                writeBatch.Delete(keyArray, familyHandle);
                mptTrie?.TryDelete(keyArray);
            }

            public override void Commit()
            {
                base.Commit();
                if (mptTrie != null)
                {
                    if (writeBatch == null)
                        throw new InvalidOperationException();

                    PutRoot(mptTrie.GetRoot(), db, writeBatch);
                }
            }
        }
    }
}
