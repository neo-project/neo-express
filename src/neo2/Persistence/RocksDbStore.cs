using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;
using System;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class RocksDbStore : Neo.Persistence.Store, IDisposable
    {
        public const string BLOCK_FAMILY = "data:block";
        public const string TX_FAMILY = "data:transaction";
        public const string MPT_FAMILY = "data:mpt";
        
        public const string ACCOUNT_FAMILY = "st:account";
        public const string UNSPENT_COIN_FAMILY = "st:coin";
        public const string SPENT_COIN_FAMILY = "st:spent-coin";
        public const string VALIDATOR_FAMILY = "st:validator";
        public const string ASSET_FAMILY = "st:asset";
        public const string CONTRACT_FAMILY = "st:contract";
        public const string STORAGE_FAMILY = "st:storage";
        public const string STATE_ROOT_FAMILY = "st:state-root";

        public const string HEADER_HASH_LIST_FAMILY = "ix:header-hash-list";
        public const string METADATA_FAMILY = "metadata";
        public const string GENERAL_STORAGE_FAMILY = "general-storage";

        public const byte VALIDATORS_COUNT_KEY = 0x90;
        public const byte BLOCK_HASH_INDEX_KEY = 0xc0;
        public const byte HEADER_HASH_INDEX_KEY = 0xc1;
        public const byte STATE_ROOT_HASH_INDEX_KEY = 0xc2;

        private static ColumnFamilies CreateColumnFamilies() 
        {
            var families = new string[] 
            {
                BLOCK_FAMILY, TX_FAMILY, MPT_FAMILY,
                ACCOUNT_FAMILY, UNSPENT_COIN_FAMILY, SPENT_COIN_FAMILY, VALIDATOR_FAMILY,
                ASSET_FAMILY, CONTRACT_FAMILY, STORAGE_FAMILY, STATE_ROOT_FAMILY,
                HEADER_HASH_LIST_FAMILY, METADATA_FAMILY, GENERAL_STORAGE_FAMILY
            };

            var columnFamilies = new ColumnFamilies();
            for (int i = 0; i < families.Length; i++)
            {
                columnFamilies.Add(families[i], new ColumnFamilyOptions());
            }
            return columnFamilies;
        }

        private readonly static Lazy<ColumnFamilies> columnFamilies = new Lazy<ColumnFamilies>(() => CreateColumnFamilies());

        public static ColumnFamilies ColumnFamilies => columnFamilies.Value;

        private readonly RocksDb db;
        private readonly Lazy<ColumnFamilyHandle> generalStorageFamily;

        public RocksDbStore(string path)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            db = RocksDb.Open(options, path, ColumnFamilies);
            generalStorageFamily = new Lazy<ColumnFamilyHandle>(() => db.GetColumnFamily(METADATA_FAMILY));
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public void CheckPoint(string path)
        {
            using var checkpoint = db.Checkpoint();
            checkpoint.Save(path);
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
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, StateRootState> GetStateRoots() => new DataCache<UInt32Wrapper, StateRootState>(db, STATE_ROOT_FAMILY);
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList() => new DataCache<UInt32Wrapper, HeaderHashList>(db, HEADER_HASH_LIST_FAMILY);
        public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> GetValidatorsCount() => new MetaDataCache<ValidatorsCountState>(db, VALIDATORS_COUNT_KEY);
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetBlockHashIndex() => new MetaDataCache<HashIndexState>(db, BLOCK_HASH_INDEX_KEY);
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetHeaderHashIndex() => new MetaDataCache<HashIndexState>(db, HEADER_HASH_INDEX_KEY);
        public override Neo.IO.Caching.MetaDataCache<RootHashIndex> GetStateRootHashIndex() => new MetaDataCache<RootHashIndex>(db, STATE_ROOT_HASH_INDEX_KEY);

        internal static byte[] GetKey(byte prefix, byte[] key)
        {
            var tempKey = new byte[key.Length + 1];
            tempKey[0] = prefix;
            key.CopyTo(tempKey, 1);
            return tempKey;
        }

        public override byte[] Get(byte prefix, byte[] key)
        {
            return db.Get(GetKey(prefix, key), generalStorageFamily.Value);
        }

        public override void Put(byte prefix, byte[] key, byte[] value)
        {
            db.Put(GetKey(prefix, key), value, generalStorageFamily.Value);
        }

        public override void PutSync(byte prefix, byte[] key, byte[] value)
        {
            db.Put(GetKey(prefix, key), value, generalStorageFamily.Value,
                new WriteOptions().SetSync(true));
        }
    }
}
