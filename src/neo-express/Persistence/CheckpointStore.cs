using Neo;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Wrappers;
using Neo.Ledger;
using OneOf;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NeoExpress.Persistence
{
    internal partial class CheckpointStore : Neo.Persistence.Store, IDisposable
    {
        internal readonly static OneOf.Types.None noneInstance = new OneOf.Types.None();

        private readonly RocksDb db;

        private static ImmutableDictionary<byte[], OneOf<T, OneOf.Types.None>> InitChangeTracker<T>() => 
            ImmutableDictionary<byte[], OneOf<T, OneOf.Types.None>>.Empty.WithComparers(new ByteArrayComparer());

        // dictionary value of None indicates the key has been deleted
        private ImmutableDictionary<byte[], OneOf<BlockState, OneOf.Types.None>> blocksTracker = InitChangeTracker<BlockState>();
        private ImmutableDictionary<byte[], OneOf<TransactionState, OneOf.Types.None>> transactionsTracker = InitChangeTracker<TransactionState>();
        private ImmutableDictionary<byte[], OneOf<AccountState, OneOf.Types.None>> accountsTracker = InitChangeTracker<AccountState>();
        private ImmutableDictionary<byte[], OneOf<UnspentCoinState, OneOf.Types.None>> unspentCoinsTracker = InitChangeTracker<UnspentCoinState>();
        private ImmutableDictionary<byte[], OneOf<SpentCoinState, OneOf.Types.None>> spentCoinsTracker = InitChangeTracker<SpentCoinState>();
        private ImmutableDictionary<byte[], OneOf<ValidatorState, OneOf.Types.None>> validatorsTracker = InitChangeTracker<ValidatorState>();
        private ImmutableDictionary<byte[], OneOf<AssetState, OneOf.Types.None>> assetsTracker = InitChangeTracker<AssetState>();
        private ImmutableDictionary<byte[], OneOf<ContractState, OneOf.Types.None>> contractsTracker = InitChangeTracker<ContractState>();
        private ImmutableDictionary<byte[], OneOf<StorageItem, OneOf.Types.None>> storagesTracker = InitChangeTracker<StorageItem>();
        private ImmutableDictionary<byte[], OneOf<HeaderHashList, OneOf.Types.None>> headerHashListTracker = InitChangeTracker<HeaderHashList>();

        // value initalized to None to indicate value hasn't been overwritten
        private OneOf<ValidatorsCountState, OneOf.Types.None> validatorsCountTracker = noneInstance;
        private OneOf<HashIndexState, OneOf.Types.None> blockHashIndexTracker = noneInstance;
        private OneOf<HashIndexState, OneOf.Types.None> headerHashIndexTracker = noneInstance;

        private readonly Dictionary<byte[], byte[]> generalStorage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public CheckpointStore(string path)
        {
            db = RocksDb.OpenReadOnly(new DbOptions(), path, RocksDbStore.ColumnFamilies, false);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public override Neo.Persistence.Snapshot GetSnapshot()
        {
            return new Snapshot(this);
        }

        public override Neo.IO.Caching.DataCache<UInt256, BlockState> GetBlocks() => new DataCache<UInt256, BlockState>(db, RocksDbStore.BLOCK_FAMILY, blocksTracker);
        public override Neo.IO.Caching.DataCache<UInt256, TransactionState> GetTransactions() => new DataCache<UInt256, TransactionState>(db, RocksDbStore.TX_FAMILY, transactionsTracker);
        public override Neo.IO.Caching.DataCache<UInt160, AccountState> GetAccounts() => new DataCache<UInt160, AccountState>(db, RocksDbStore.ACCOUNT_FAMILY, accountsTracker);
        public override Neo.IO.Caching.DataCache<UInt256, UnspentCoinState> GetUnspentCoins() => new DataCache<UInt256, UnspentCoinState>(db, RocksDbStore.UNSPENT_COIN_FAMILY, unspentCoinsTracker);
        public override Neo.IO.Caching.DataCache<UInt256, SpentCoinState> GetSpentCoins() => new DataCache<UInt256, SpentCoinState>(db, RocksDbStore.SPENT_COIN_FAMILY, spentCoinsTracker);
        public override Neo.IO.Caching.DataCache<ECPoint, ValidatorState> GetValidators() => new DataCache<ECPoint, ValidatorState>(db, RocksDbStore.VALIDATOR_FAMILY, validatorsTracker);
        public override Neo.IO.Caching.DataCache<UInt256, AssetState> GetAssets() => new DataCache<UInt256, AssetState>(db, RocksDbStore.ASSET_FAMILY, assetsTracker);
        public override Neo.IO.Caching.DataCache<UInt160, ContractState> GetContracts() => new DataCache<UInt160, ContractState>(db, RocksDbStore.CONTRACT_FAMILY, contractsTracker);
        public override Neo.IO.Caching.DataCache<StorageKey, StorageItem> GetStorages() => new DataCache<StorageKey, StorageItem>(db, RocksDbStore.STORAGE_FAMILY, storagesTracker);
        public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList() => new DataCache<UInt32Wrapper, HeaderHashList>(db, RocksDbStore.HEADER_HASH_LIST_FAMILY, headerHashListTracker);
        public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> GetValidatorsCount() => new MetaDataCache<ValidatorsCountState>(db, RocksDbStore.VALIDATORS_COUNT_KEY, validatorsCountTracker);
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetBlockHashIndex() => new MetaDataCache<HashIndexState>(db, RocksDbStore.CURRENT_BLOCK_KEY, blockHashIndexTracker);
        public override Neo.IO.Caching.MetaDataCache<HashIndexState> GetHeaderHashIndex() => new MetaDataCache<HashIndexState>(db, RocksDbStore.CURRENT_HEADER_KEY, headerHashIndexTracker);

        private void UpdateBlocks(UInt256 key, OneOf<BlockState, OneOf.Types.None> value) => blocksTracker = blocksTracker.SetItem(key.ToArray(), value);
        private void UpdateTransactions(UInt256 key, OneOf<TransactionState, OneOf.Types.None> value) => transactionsTracker = transactionsTracker.SetItem(key.ToArray(), value);
        private void UpdateAccounts(UInt160 key, OneOf<AccountState, OneOf.Types.None> value) => accountsTracker = accountsTracker.SetItem(key.ToArray(), value);
        private void UpdateUnspentCoins(UInt256 key, OneOf<UnspentCoinState, OneOf.Types.None> value) => unspentCoinsTracker = unspentCoinsTracker.SetItem(key.ToArray(), value);
        private void UpdateSpentCoins(UInt256 key, OneOf<SpentCoinState, OneOf.Types.None> value) => spentCoinsTracker = spentCoinsTracker.SetItem(key.ToArray(), value);
        private void UpdateValidators(ECPoint key, OneOf<ValidatorState, OneOf.Types.None> value) => validatorsTracker = validatorsTracker.SetItem(key.ToArray(), value);
        private void UpdateAssets(UInt256 key, OneOf<AssetState, OneOf.Types.None> value) => assetsTracker = assetsTracker.SetItem(key.ToArray(), value);
        private void UpdateContracts(UInt160 key, OneOf<ContractState, OneOf.Types.None> value) => contractsTracker = contractsTracker.SetItem(key.ToArray(), value);
        private void UpdateStorages(StorageKey key, OneOf<StorageItem, OneOf.Types.None> value) => storagesTracker = storagesTracker.SetItem(key.ToArray(), value);
        private void UpdateHeaderHashList(UInt32Wrapper key, OneOf<HeaderHashList, OneOf.Types.None> value) => headerHashListTracker = headerHashListTracker.SetItem(key.ToArray(), value);
        private void UpdateValidatorsCount(ValidatorsCountState value) => validatorsCountTracker = value;
        private void UpdateBlockHashIndex(HashIndexState value) => blockHashIndexTracker = value;
        private void UpdateHeaderHashIndex(HashIndexState value) => headerHashIndexTracker = value;

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
