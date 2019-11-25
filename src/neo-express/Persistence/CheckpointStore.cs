using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace NeoExpress.Persistence
{
    internal partial class CheckpointStore : Neo.Persistence.Store, IDisposable
    {

        internal readonly static OneOf.Types.None NONE_INSTANCE = new OneOf.Types.None();

        private readonly RocksDb db;

        private readonly DataTracker<UInt256, BlockState> _blocks;
        private readonly DataTracker<UInt256, TransactionState> _transactions;
        private readonly DataTracker<UInt160, AccountState> _accounts;
        private readonly DataTracker<UInt256, UnspentCoinState> _unspentCoins;
        private readonly DataTracker<UInt256, SpentCoinState> _spentCoins;
        private readonly DataTracker<ECPoint, ValidatorState> _validators;
        private readonly DataTracker<UInt256, AssetState> _assets;
        private readonly DataTracker<UInt160, ContractState> _contracts;
        private readonly DataTracker<StorageKey, StorageItem> _storages;
        private readonly DataTracker<UInt32Wrapper, HeaderHashList> _headerHashList;

        private readonly MetadataTracker<ValidatorsCountState> _validatorsCount;
        private readonly MetadataTracker<HashIndexState> _blockHashIndex;
        private readonly MetadataTracker<HashIndexState> _headerHashIndex;

        private readonly Dictionary<byte[], byte[]> generalStorage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public CheckpointStore(string path)
        {
            db = RocksDb.OpenReadOnly(new DbOptions(), path, RocksDbStore.ColumnFamilies, false);
            var metadataColumnHandle = db.GetColumnFamily(RocksDbStore.METADATA_FAMILY);

            _blocks = new DataTracker<UInt256, BlockState>(db, RocksDbStore.BLOCK_FAMILY);
            _transactions = new DataTracker<UInt256, TransactionState>(db, RocksDbStore.TX_FAMILY);
            _accounts = new DataTracker<UInt160, AccountState>(db, RocksDbStore.ACCOUNT_FAMILY);
            _unspentCoins = new DataTracker<UInt256, UnspentCoinState>(db, RocksDbStore.UNSPENT_COIN_FAMILY);
            _spentCoins = new DataTracker<UInt256, SpentCoinState>(db, RocksDbStore.SPENT_COIN_FAMILY);
            _validators = new DataTracker<ECPoint, ValidatorState>(db, RocksDbStore.VALIDATOR_FAMILY);
            _assets = new DataTracker<UInt256, AssetState>(db, RocksDbStore.ASSET_FAMILY);
            _contracts = new DataTracker<UInt160, ContractState>(db, RocksDbStore.CONTRACT_FAMILY);
            _storages = new DataTracker<StorageKey, StorageItem>(db, RocksDbStore.STORAGE_FAMILY);
            _headerHashList = new DataTracker<UInt32Wrapper, HeaderHashList>(db, RocksDbStore.HEADER_HASH_LIST_FAMILY);
            _validatorsCount = new MetadataTracker<ValidatorsCountState>(db, RocksDbStore.VALIDATORS_COUNT_KEY, metadataColumnHandle);
            _blockHashIndex = new MetadataTracker<HashIndexState>(db, RocksDbStore.CURRENT_BLOCK_KEY, metadataColumnHandle);
            _headerHashIndex = new MetadataTracker<HashIndexState>(db, RocksDbStore.CURRENT_HEADER_KEY, metadataColumnHandle);
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
            => _blocks.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, TransactionState> GetTransactions() 
            => _transactions.GetCache();
        public override Neo.IO.Caching.DataCache<UInt160, AccountState> GetAccounts() 
            => _accounts.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, UnspentCoinState> GetUnspentCoins() 
            => _unspentCoins.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, SpentCoinState> GetSpentCoins() 
            => _spentCoins.GetCache();
        public override Neo.IO.Caching.DataCache<ECPoint, ValidatorState> GetValidators()
            => _validators.GetCache();
        public override Neo.IO.Caching.DataCache<UInt256, AssetState> GetAssets() 
            => _assets.GetCache();
        public override Neo.IO.Caching.DataCache<UInt160, ContractState> GetContracts()
            => _contracts.GetCache();
        public override Neo.IO.Caching.DataCache<StorageKey, StorageItem> GetStorages() 
            => _storages.GetCache();
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList() 
            => _headerHashList.GetCache();
        public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> GetValidatorsCount() 
            => _validatorsCount.GetCache();
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetBlockHashIndex() 
            => _blockHashIndex.GetCache();
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetHeaderHashIndex() 
            => _headerHashIndex.GetCache();

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
