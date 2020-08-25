using Neo.Trie;
using RocksDbSharp;

namespace NeoExpress.Neo2.Persistence
{
    partial class RocksDbStore
    {
        private class KVStore : IKVStore
        {
            private readonly RocksDb db;
            private readonly ColumnFamilyHandle familyHandle;
            private readonly ReadOptions readOptions;
            private readonly WriteBatch writeBatch;

            public KVStore(RocksDb db, string familyName, ReadOptions readOptions, WriteBatch writeBatch)
                : this(db, db.GetColumnFamily(familyName), readOptions, writeBatch)
            {
            }

            public KVStore(RocksDb db, ColumnFamilyHandle familyHandle, ReadOptions readOptions, WriteBatch writeBatch)
            {
                this.db = db;
                this.familyHandle = familyHandle;
                this.readOptions = readOptions;
                this.writeBatch = writeBatch;
            }

            public byte[] Get(byte[] key) => db.Get(key, familyHandle, readOptions);

            public void Put(byte[] key, byte[] value) => _ = writeBatch.Put(key, value, familyHandle);
        }
    }
}
