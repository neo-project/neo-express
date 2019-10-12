using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;
using System;
using System.Collections.Generic;

#nullable enable

namespace NeoExpress.Persistence
{
    internal partial class CheckpointStore : Neo.Persistence.Store, IDisposable
    {
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

        private static DataCache<TKey, TValue> GetDataCache<TKey, TValue>(RocksDb db, string familyName)
            where TKey : IEquatable<TKey>, Neo.IO.ISerializable, new()
            where TValue : class, Neo.IO.ICloneable<TValue>, Neo.IO.ISerializable, new()
        {
            var columnFamily = db.GetColumnFamily(familyName);
            return new DataCache<TKey, TValue>(db, columnFamily);
        }

        private static MetaDataCache<T> GetMetaDataCache<T>(
            RocksDb db, byte key)
            where T : class, Neo.IO.ICloneable<T>, Neo.IO.ISerializable, new()
        {
            var columnFamily = db.GetColumnFamily(RocksDbStore.METADATA_FAMILY);
            var keyArray = new byte[1] { key };
            return new MetaDataCache<T>(db, keyArray, columnFamily);
        }

        public CheckpointStore(string path)
        {
            db = RocksDb.OpenReadOnly(new DbOptions(), path, RocksDbStore.ColumnFamilies, false);

            blocks = GetDataCache<UInt256, BlockState>(db, RocksDbStore.BLOCK_FAMILY);
            transactions = GetDataCache<UInt256, TransactionState>(db, RocksDbStore.TX_FAMILY);
            accounts = GetDataCache<UInt160, AccountState>(db, RocksDbStore.ACCOUNT_FAMILY);
            unspentCoins = GetDataCache<UInt256, UnspentCoinState>(db, RocksDbStore.UNSPENT_COIN_FAMILY);
            spentCoins = GetDataCache<UInt256, SpentCoinState>(db, RocksDbStore.SPENT_COIN_FAMILY);
            validators = GetDataCache<ECPoint, ValidatorState>(db, RocksDbStore.VALIDATOR_FAMILY);
            assets = GetDataCache<UInt256, AssetState>(db, RocksDbStore.ASSET_FAMILY);
            contracts = GetDataCache<UInt160, ContractState>(db, RocksDbStore.CONTRACT_FAMILY);
            storages = GetDataCache<StorageKey, StorageItem>(db, RocksDbStore.STORAGE_FAMILY);
            headerHashList = GetDataCache<UInt32Wrapper, HeaderHashList>(db, RocksDbStore.HEADER_HASH_LIST_FAMILY);
            validatorsCount = GetMetaDataCache<ValidatorsCountState>(db, RocksDbStore.VALIDATORS_COUNT_KEY);
            blockHashIndex = GetMetaDataCache<HashIndexState>(db, RocksDbStore.CURRENT_BLOCK_KEY);
            headerHashIndex = GetMetaDataCache<HashIndexState>(db, RocksDbStore.CURRENT_HEADER_KEY);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public override Neo.Persistence.Snapshot GetSnapshot()
        {
            return new Snapshot(this);
        }

        public override Neo.IO.Caching.DataCache<UInt256, BlockState> GetBlocks() => blocks;
        public override Neo.IO.Caching.DataCache<UInt256, TransactionState> GetTransactions() => transactions;
        public override Neo.IO.Caching.DataCache<UInt160, AccountState> GetAccounts() => accounts;
        public override Neo.IO.Caching.DataCache<UInt256, UnspentCoinState> GetUnspentCoins() => unspentCoins;
        public override Neo.IO.Caching.DataCache<UInt256, SpentCoinState> GetSpentCoins() => spentCoins;
        public override Neo.IO.Caching.DataCache<ECPoint, ValidatorState> GetValidators() => validators;
        public override Neo.IO.Caching.DataCache<UInt256, AssetState> GetAssets() => assets;
        public override Neo.IO.Caching.DataCache<UInt160, ContractState> GetContracts() => contracts;
        public override Neo.IO.Caching.DataCache<StorageKey, StorageItem> GetStorages() => storages;
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList() => headerHashList;
        public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> GetValidatorsCount() => validatorsCount;
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetBlockHashIndex() => blockHashIndex;
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetHeaderHashIndex() => headerHashIndex;

        private readonly Dictionary<byte[], byte[]> generalStorage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public override byte[] Get(byte prefix, byte[] key)
        {
            var keyArray = RocksDbStore.GetKey(prefix, key);
            if (generalStorage.TryGetValue(keyArray, out var value))
            {
                return value;
            }

            var columnFamily = db.GetColumnFamily(RocksDbStore.GENERAL_STORAGE_FAMILY);
            return db.Get(keyArray, columnFamily);
        }

        public override void Put(byte prefix, byte[] key, byte[] value)
        {
            var keyArray = RocksDbStore.GetKey(prefix, key);
            generalStorage[keyArray] = value;
        }

        public override void PutSync(byte prefix, byte[] key, byte[] value)
        {
            Put(prefix, key, value);
        }
    }
}
