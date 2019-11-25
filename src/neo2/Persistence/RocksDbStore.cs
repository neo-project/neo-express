using Neo;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;
using System;

namespace Neo2Express.Persistence
{
    internal partial class RocksDbStore : Neo.Persistence.Store, IDisposable
    {
        public const string BLOCK_FAMILY = "data:block";
        public const string TX_FAMILY = "data:transaction";
        public const string ACCOUNT_FAMILY = "st:account";
        public const string ASSET_FAMILY = "st:asset";
        public const string CONTRACT_FAMILY = "st:contract";
        public const string HEADER_HASH_LIST_FAMILY = "ix:header-hash-list";
        public const string SPENT_COIN_FAMILY = "st:spent-coin";
        public const string STORAGE_FAMILY = "st:storage";
        public const string UNSPENT_COIN_FAMILY = "st:coin";
        public const string VALIDATOR_FAMILY = "st:validator";
        public const string METADATA_FAMILY = "metadata";
        public const string GENERAL_STORAGE_FAMILY = "general-storage";

        public const byte VALIDATORS_COUNT_KEY = 0x90;
        public const byte CURRENT_BLOCK_KEY = 0xc0;
        public const byte CURRENT_HEADER_KEY = 0xc1;

        public static ColumnFamilies ColumnFamilies => new ColumnFamilies {
                { BLOCK_FAMILY, new ColumnFamilyOptions() },
                { TX_FAMILY, new ColumnFamilyOptions() },
                { ACCOUNT_FAMILY, new ColumnFamilyOptions() },
                { UNSPENT_COIN_FAMILY, new ColumnFamilyOptions() },
                { SPENT_COIN_FAMILY, new ColumnFamilyOptions() },
                { VALIDATOR_FAMILY, new ColumnFamilyOptions() },
                { ASSET_FAMILY, new ColumnFamilyOptions() },
                { CONTRACT_FAMILY, new ColumnFamilyOptions() },
                { STORAGE_FAMILY, new ColumnFamilyOptions() },
                { HEADER_HASH_LIST_FAMILY, new ColumnFamilyOptions() },
                { METADATA_FAMILY, new ColumnFamilyOptions() },
                { GENERAL_STORAGE_FAMILY, new ColumnFamilyOptions() }};

        private readonly RocksDb db;

        public RocksDbStore(string path)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            db = RocksDb.Open(options, path, ColumnFamilies);

            var writeBatch = new WriteBatch();
            var readOptions = new ReadOptions().SetFillCache(true);
            using (Iterator it = db.NewIterator(readOptions: readOptions))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    writeBatch.Delete(it.Key());
                }
            }
            db.Write(writeBatch);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public void CheckPoint(string path)
        {
            using (var checkpoint = db.Checkpoint())
            {
                checkpoint.Save(path);
            }
        }

        public override Neo.Persistence.Snapshot GetSnapshot() => new Snapshot(db);

        public override Neo.IO.Caching.DataCache<UInt256, BlockState> GetBlocks() => new DataCache<UInt256, BlockState>(db, BLOCK_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt256, TransactionState> GetTransactions() => new DataCache<UInt256, TransactionState>(db, TX_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt160, AccountState> GetAccounts() => new DataCache<UInt160, AccountState>(db, ACCOUNT_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt256, UnspentCoinState> GetUnspentCoins() => new DataCache<UInt256, UnspentCoinState>(db, UNSPENT_COIN_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt256, SpentCoinState> GetSpentCoins() => new DataCache<UInt256, SpentCoinState>(db, SPENT_COIN_FAMILY);
        public override Neo.IO.Caching.DataCache<ECPoint, ValidatorState> GetValidators() => new DataCache<ECPoint, ValidatorState>(db, VALIDATOR_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt256, AssetState> GetAssets() => new DataCache<UInt256, AssetState>(db, ASSET_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt160, ContractState> GetContracts() => new DataCache<UInt160, ContractState>(db, CONTRACT_FAMILY);
        public override Neo.IO.Caching.DataCache<StorageKey, StorageItem> GetStorages() => new DataCache<StorageKey, StorageItem>(db, STORAGE_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList() => new DataCache<UInt32Wrapper, HeaderHashList>(db, HEADER_HASH_LIST_FAMILY);
        public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> GetValidatorsCount() => new MetaDataCache<ValidatorsCountState>(db, VALIDATORS_COUNT_KEY);
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetBlockHashIndex() => new MetaDataCache<HashIndexState>(db, CURRENT_BLOCK_KEY);
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetHeaderHashIndex() => new MetaDataCache<HashIndexState>(db, CURRENT_HEADER_KEY);

        internal static byte[] GetKey(byte prefix, byte[] key)
        {
            var tempKey = new byte[key.Length + 1];
            tempKey[0] = prefix;
            key.CopyTo(tempKey, 1);
            return tempKey;
        }

        public override byte[] Get(byte prefix, byte[] key)
        {
            var columnFamily = db.GetColumnFamily(GENERAL_STORAGE_FAMILY);
            return db.Get(GetKey(prefix, key), columnFamily);
        }

        public override void Put(byte prefix, byte[] key, byte[] value)
        {
            var columnFamily = db.GetColumnFamily(GENERAL_STORAGE_FAMILY);
            db.Put(GetKey(prefix, key), value, columnFamily);
        }

        public override void PutSync(byte prefix, byte[] key, byte[] value)
        {
            var columnFamily = db.GetColumnFamily(GENERAL_STORAGE_FAMILY);
            db.Put(GetKey(prefix, key), value, columnFamily,
                new WriteOptions().SetSync(true));
        }
    }
}
