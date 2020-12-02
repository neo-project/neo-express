using Neo.Trie;
using OneOf;
using System.Collections.Immutable;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class CheckpointStore
    {
        private class KVStore : IKVStore
        {
            private readonly KVTracker tracker;
            private readonly ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>> snapshot;

            public KVStore(KVTracker tracker, ImmutableDictionary<byte[], OneOf<byte[], OneOf.Types.None>> snapshot)
            {
                this.tracker = tracker;
                this.snapshot = snapshot;
            }

            public byte[]? Get(byte[] key)
            {
                return tracker.TryGet(key, snapshot);

            }

            public void Put(byte[] key, byte[] value)
            {
                tracker.Update(key, value);
            }
        }
    }
}
