using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;
using System;

namespace Neo.Express.Persistence
{
    internal partial class RocksDbStore : Neo.Persistence.Store, IDisposable
    {
        private const string BLOCK_FAMILY = "data:block";
        private const string TX_FAMILY = "data:transaction";
        private const string ACCOUNT_FAMILY = "st:account";
        private const string ASSET_FAMILY = "st:asset";
        private const string CONTRACT_FAMILY = "st:contract";
        private const string HEADER_HASH_LIST_FAMILY = "ix:header-hash-list";
        private const string SPENT_COIN_FAMILY = "st:spent-coin";
        private const string STORAGE_FAMILY = "st:storage";
        private const string UNSPENT_COIN_FAMILY = "st:coin";
        private const string VALIDATOR_FAMILY = "st:validator";
        private const string METADATA_FAMILY = "metadata";
        private const string GENERAL_STORAGE_FAMILY = "general-storage";

        private const byte VALIDATORS_COUNT_KEY = 0x90;
        private const byte CURRENT_BLOCK_KEY = 0xc0;
        private const byte CURRENT_HEADER_KEY = 0xc1;

        private static DataCache<TKey, TValue> GetDataCache<TKey, TValue>(
            RocksDb db, string familyName, ReadOptions readOptions = null, WriteBatch writeBatch = null)
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            var columnFamily = db.GetColumnFamily(familyName);
            return new DataCache<TKey, TValue>(db, columnFamily, readOptions, writeBatch);
        }

        private static MetaDataCache<T> GetMetaDataCache<T>(
            RocksDb db, byte key, ReadOptions readOptions = null, WriteBatch writeBatch = null)
            where T : class, ICloneable<T>, ISerializable, new()
        {
            var columnFamily = db.GetColumnFamily(METADATA_FAMILY);
            var keyArray = new byte[1] { key };
            return new MetaDataCache<T>(db, keyArray, columnFamily, readOptions, writeBatch);
        }

        private readonly RocksDb db;

        private readonly DataCache<UInt256, BlockState> blocks;
        private readonly DataCache<UInt256, TransactionState> transactions;
        private readonly DataCache<UInt160, AccountState> accounts;
        private readonly DataCache<UInt256, UnspentCoinState> unspentCoins;
        private readonly DataCache<UInt256, SpentCoinState> spentCoins;
        private readonly DataCache<ECPoint, ValidatorState> validators;
        private readonly DataCache<UInt256, AssetState> assets;
        private readonly DataCache<UInt160, ContractState> contracts;
        private readonly DataCache<StorageKey, StorageItem> storages;
        private readonly DataCache<UInt32Wrapper, HeaderHashList> headerHashList;
        private readonly MetaDataCache<ValidatorsCountState> validatorsCount;
        private readonly MetaDataCache<HashIndexState> blockHashIndex;
        private readonly MetaDataCache<HashIndexState> headerHashIndex;

        public RocksDbStore(string path, bool readOnly = false)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            var columnFamilies = new ColumnFamilies {
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
                { GENERAL_STORAGE_FAMILY, new ColumnFamilyOptions() }
            };

            db = readOnly
                ? RocksDb.OpenReadOnly(options, path, columnFamilies, true)
                : RocksDb.Open(options, path, columnFamilies);

            blocks = GetDataCache<UInt256, BlockState>(db, BLOCK_FAMILY);
            transactions = GetDataCache<UInt256, TransactionState>(db, TX_FAMILY);
            accounts = GetDataCache<UInt160, AccountState>(db, ACCOUNT_FAMILY);
            unspentCoins = GetDataCache<UInt256, UnspentCoinState>(db, UNSPENT_COIN_FAMILY);
            spentCoins = GetDataCache<UInt256, SpentCoinState>(db, SPENT_COIN_FAMILY);
            validators = GetDataCache<ECPoint, ValidatorState>(db, VALIDATOR_FAMILY);
            assets = GetDataCache<UInt256, AssetState>(db, ASSET_FAMILY);
            contracts = GetDataCache<UInt160, ContractState>(db, CONTRACT_FAMILY);
            storages = GetDataCache<StorageKey, StorageItem>(db, STORAGE_FAMILY);
            headerHashList = GetDataCache<UInt32Wrapper, HeaderHashList>(db, HEADER_HASH_LIST_FAMILY);
            validatorsCount = GetMetaDataCache<ValidatorsCountState>(db, VALIDATORS_COUNT_KEY);
            blockHashIndex = GetMetaDataCache<HashIndexState>(db, CURRENT_BLOCK_KEY);
            headerHashIndex = GetMetaDataCache<HashIndexState>(db, CURRENT_HEADER_KEY);

            if (!readOnly)
            {
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

        public override IO.Caching.DataCache<UInt256, BlockState> GetBlocks() => blocks;
        public override IO.Caching.DataCache<UInt256, TransactionState> GetTransactions() => transactions;
        public override IO.Caching.DataCache<UInt160, AccountState> GetAccounts() => accounts;
        public override IO.Caching.DataCache<UInt256, UnspentCoinState> GetUnspentCoins() => unspentCoins;
        public override IO.Caching.DataCache<UInt256, SpentCoinState> GetSpentCoins() => spentCoins;
        public override IO.Caching.DataCache<ECPoint, ValidatorState> GetValidators() => validators;
        public override IO.Caching.DataCache<UInt256, AssetState> GetAssets() => assets;
        public override IO.Caching.DataCache<UInt160, ContractState> GetContracts() => contracts;
        public override IO.Caching.DataCache<StorageKey, StorageItem> GetStorages() => storages;
        public override IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList() => headerHashList;
        public override IO.Caching.MetaDataCache<ValidatorsCountState> GetValidatorsCount() => validatorsCount;
        public override IO.Caching.MetaDataCache<HashIndexState> GetBlockHashIndex() => blockHashIndex;
        public override IO.Caching.MetaDataCache<HashIndexState> GetHeaderHashIndex() => headerHashIndex;

        private static byte[] GetKey(byte prefix, byte[] key)
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
