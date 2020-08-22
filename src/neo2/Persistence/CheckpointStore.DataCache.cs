using Neo;
using Neo.IO;
using Neo.Trie.MPT;
using OneOf;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class CheckpointStore
    {
        private class DataCache<TKey, TValue> : Neo.IO.Caching.DataCache<TKey, TValue>
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            private readonly bool updateEnabled;
            private readonly DataTracker<TKey, TValue> tracker;
            private readonly ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>>? snapshot = null;
            private readonly MPTTrie? mptTrie = null;

            public DataCache(DataTracker<TKey, TValue> tracker)
            {
                this.tracker = tracker;
            }

            public DataCache(DataTracker<TKey, TValue> tracker,
                ImmutableDictionary<byte[], OneOf<TValue, OneOf.Types.None>> values,
                MPTTrie? mptTrie)
            {
                this.updateEnabled = true;
                this.tracker = tracker;
                this.snapshot = values;
                this.mptTrie = mptTrie;
            }

            protected override IEnumerable<KeyValuePair<TKey, TValue>> FindInternal(byte[] key_prefix)
            {
                return tracker.Find(key_prefix, snapshot);
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

#pragma warning disable CS8764 // Nullability of reference types in return type doesn't match overridden member.
            protected override TValue? TryGetInternal(TKey key)
#pragma warning restore CS8764 // Nullability of reference types in return type doesn't match overridden member.
            {
                return tracker.TryGet(key, snapshot);
            }

            public override void DeleteInternal(TKey key)
            {
                if (!updateEnabled)
                    throw new InvalidOperationException();

                var keyArray = key.ToArray();
                tracker.Update(keyArray, NONE_INSTANCE);
                mptTrie?.TryDelete(keyArray);
            }

            protected override void AddInternal(TKey key, TValue value)
            {
                UpdateInternal(key, value);
            }

            protected override void UpdateInternal(TKey key, TValue value)
            {
                if (!updateEnabled)
                    throw new InvalidOperationException();

                var keyArray = key.ToArray();
                tracker.Update(keyArray, value);
                mptTrie?.Put(keyArray, value.ToArray());
            }

            public override void Commit()
            {
                base.Commit();
                if (mptTrie != null)
                {
                    if (!updateEnabled)
                        throw new InvalidOperationException();

                    tracker.PutRoot(mptTrie.GetRoot());
                }
            }
        }
    }
}
