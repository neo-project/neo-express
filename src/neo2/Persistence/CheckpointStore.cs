using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class CheckpointStore : Neo.Persistence.Store, IDisposable
    {

        internal readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();

        private readonly RocksDb db;

        private readonly DataTracker<UInt256, BlockState> blocks;
        private readonly DataTracker<UInt256, TransactionState> transactions;
        private readonly DataTracker<UInt160, AccountState> accounts;
        private readonly DataTracker<UInt256, UnspentCoinState> unspentCoins;
        private readonly DataTracker<UInt256, SpentCoinState> spentCoins;
        private readonly DataTracker<ECPoint, ValidatorState> validators;
        private readonly DataTracker<UInt256, AssetState> assets;
        private readonly DataTracker<UInt160, ContractState> contracts;
        private readonly DataTracker<StorageKey, StorageItem> storages;
        private readonly DataTracker<UInt32Wrapper, StateRootState> stateRoots;
        private readonly DataTracker<UInt32Wrapper, HeaderHashList> headerHashList;

        private readonly MetadataTracker<ValidatorsCountState> validatorsCount;
        private readonly MetadataTracker<HashIndexState> blockHashIndex;
        private readonly MetadataTracker<HashIndexState> headerHashIndex;
        private readonly MetadataTracker<RootHashIndex> stateRootHashIndex;

        private readonly Dictionary<byte[], byte[]> generalStorage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public CheckpointStore(string path)
        {
            db = RocksDb.OpenReadOnly(new DbOptions(), path, RocksDbStore.ColumnFamilies, false);
            var metadataColumnHandle = db.GetColumnFamily(RocksDbStore.METADATA_FAMILY);

            blocks = new DataTracker<UInt256, BlockState>(db, RocksDbStore.BLOCK_FAMILY);
            transactions = new DataTracker<UInt256, TransactionState>(db, RocksDbStore.TX_FAMILY);
            accounts = new DataTracker<UInt160, AccountState>(db, RocksDbStore.ACCOUNT_FAMILY);
            unspentCoins = new DataTracker<UInt256, UnspentCoinState>(db, RocksDbStore.UNSPENT_COIN_FAMILY);
            spentCoins = new DataTracker<UInt256, SpentCoinState>(db, RocksDbStore.SPENT_COIN_FAMILY);
            validators = new DataTracker<ECPoint, ValidatorState>(db, RocksDbStore.VALIDATOR_FAMILY);
            assets = new DataTracker<UInt256, AssetState>(db, RocksDbStore.ASSET_FAMILY);
            contracts = new DataTracker<UInt160, ContractState>(db, RocksDbStore.CONTRACT_FAMILY);
            storages = new DataTracker<StorageKey, StorageItem>(db, RocksDbStore.STORAGE_FAMILY);
            stateRoots = new DataTracker<UInt32Wrapper, StateRootState>(db, RocksDbStore.STATE_ROOT_FAMILY);
            headerHashList = new DataTracker<UInt32Wrapper, HeaderHashList>(db, RocksDbStore.HEADER_HASH_LIST_FAMILY);
            validatorsCount = new MetadataTracker<ValidatorsCountState>(db, RocksDbStore.VALIDATORS_COUNT_KEY, metadataColumnHandle);
            blockHashIndex = new MetadataTracker<HashIndexState>(db, RocksDbStore.CURRENT_BLOCK_KEY, metadataColumnHandle);
            headerHashIndex = new MetadataTracker<HashIndexState>(db, RocksDbStore.CURRENT_HEADER_KEY, metadataColumnHandle);
            stateRootHashIndex = new MetadataTracker<RootHashIndex>(db, RocksDbStore.CURRENT_ROOT_KEY, metadataColumnHandle);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public override Neo.Persistence.Snapshot GetSnapshot()
        {
            return new Snapshot(this);
        }

        public override Neo.IO.Caching.DataCache<UInt256, BlockState> GetBlocks()
            => blocks.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, TransactionState> GetTransactions()
            => transactions.GetCache();
        public override Neo.IO.Caching.DataCache<UInt160, AccountState> GetAccounts()
            => accounts.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, UnspentCoinState> GetUnspentCoins()
            => unspentCoins.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, SpentCoinState> GetSpentCoins()
            => spentCoins.GetCache();
        public override Neo.IO.Caching.DataCache<ECPoint, ValidatorState> GetValidators()
            => validators.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, AssetState> GetAssets()
            => assets.GetCache();
        public override Neo.IO.Caching.DataCache<UInt160, ContractState> GetContracts()
            => contracts.GetCache();
        public override Neo.IO.Caching.DataCache<StorageKey, StorageItem> GetStorages()
            => storages.GetCache();
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, StateRootState> GetStateRoots()
            => stateRoots.GetCache();
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList()
            => headerHashList.GetCache();
        public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> GetValidatorsCount()
            => validatorsCount.GetCache();
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetBlockHashIndex()
            => blockHashIndex.GetCache();
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetHeaderHashIndex()
            => headerHashIndex.GetCache();
        public override Neo.IO.Caching.MetaDataCache<RootHashIndex> GetStateRootHashIndex()
            => stateRootHashIndex.GetCache();

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
