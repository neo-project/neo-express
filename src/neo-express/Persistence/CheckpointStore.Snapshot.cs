using System;
using System.Collections.Generic;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Wrappers;
using Neo.Ledger;
using OneOf;
using RocksDbSharp;

namespace NeoExpress.Persistence
{
    internal partial class CheckpointStore
    {
        private class Snapshot : Neo.Persistence.Snapshot
        {
            public Snapshot(CheckpointStore store)
            {
                Blocks = new DataCache<UInt256, BlockState>(store.db, RocksDbStore.BLOCK_FAMILY, store.blocksTracker, store.UpdateBlocks);
                Transactions = new DataCache<UInt256, TransactionState>(store.db, RocksDbStore.TX_FAMILY, store.transactionsTracker, store.UpdateTransactions);
                Accounts = new DataCache<UInt160, AccountState>(store.db, RocksDbStore.ACCOUNT_FAMILY, store.accountsTracker, store.UpdateAccounts);
                UnspentCoins = new DataCache<UInt256, UnspentCoinState>(store.db, RocksDbStore.UNSPENT_COIN_FAMILY, store.unspentCoinsTracker, store.UpdateUnspentCoins);
                SpentCoins = new DataCache<UInt256, SpentCoinState>(store.db, RocksDbStore.SPENT_COIN_FAMILY, store.spentCoinsTracker, store.UpdateSpentCoins);
                Validators = new DataCache<ECPoint, ValidatorState>(store.db, RocksDbStore.VALIDATOR_FAMILY, store.validatorsTracker, store.UpdateValidators);
                Assets = new DataCache<UInt256, AssetState>(store.db, RocksDbStore.ASSET_FAMILY, store.assetsTracker, store.UpdateAssets);
                Contracts = new DataCache<UInt160, ContractState>(store.db, RocksDbStore.CONTRACT_FAMILY, store.contractsTracker, store.UpdateContracts);
                Storages = new DataCache<StorageKey, StorageItem>(store.db, RocksDbStore.STORAGE_FAMILY, store.storagesTracker, store.UpdateStorages);
                HeaderHashList = new DataCache<UInt32Wrapper, HeaderHashList>(store.db, RocksDbStore.HEADER_HASH_LIST_FAMILY, store.headerHashListTracker, store.UpdateHeaderHashList);

                ValidatorsCount = new MetaDataCache<ValidatorsCountState>(store.db, RocksDbStore.VALIDATORS_COUNT_KEY, store.validatorsCountTracker, store.UpdateValidatorsCount);
                BlockHashIndex = new MetaDataCache<HashIndexState>(store.db, RocksDbStore.CURRENT_BLOCK_KEY, store.blockHashIndexTracker, store.UpdateBlockHashIndex);
                HeaderHashIndex = new MetaDataCache<HashIndexState>(store.db, RocksDbStore.CURRENT_HEADER_KEY, store.headerHashIndexTracker, store.UpdateHeaderHashIndex);
            }

            public override Neo.IO.Caching.DataCache<UInt256, BlockState> Blocks { get; }
            public override Neo.IO.Caching.DataCache<UInt256, TransactionState> Transactions { get; }
            public override Neo.IO.Caching.DataCache<UInt160, AccountState> Accounts { get; }
            public override Neo.IO.Caching.DataCache<UInt256, UnspentCoinState> UnspentCoins { get; }
            public override Neo.IO.Caching.DataCache<UInt256, SpentCoinState> SpentCoins { get; }
            public override Neo.IO.Caching.DataCache<ECPoint, ValidatorState> Validators { get; }
            public override Neo.IO.Caching.DataCache<UInt256, AssetState> Assets { get; }
            public override Neo.IO.Caching.DataCache<UInt160, ContractState> Contracts { get; }
            public override Neo.IO.Caching.DataCache<StorageKey, StorageItem> Storages { get; }
            public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList { get; }
            public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> ValidatorsCount { get; }
            public override Neo.IO.Caching.MetaDataCache<HashIndexState> BlockHashIndex { get; }
            public override Neo.IO.Caching.MetaDataCache<HashIndexState> HeaderHashIndex { get; }
        }
    }
}
