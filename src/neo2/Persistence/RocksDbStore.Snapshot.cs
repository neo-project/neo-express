﻿using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Wrappers;
using Neo.Ledger;
using Neo.Trie.MPT;
using RocksDbSharp;
using System;

namespace NeoExpress.Neo2.Persistence
{
    partial class RocksDbStore
    {
        private class Snapshot : Neo.Persistence.Snapshot
        {
            private readonly RocksDb db;
            private readonly RocksDbSharp.Snapshot snapshot;
            private readonly ReadOptions readOptions;
            private readonly WriteBatch writeBatch;

            public Snapshot(RocksDb db)
            {
                this.db = db;
                snapshot = db.CreateSnapshot();
                readOptions = new ReadOptions().SetSnapshot(snapshot).SetFillCache(false);
                writeBatch = new WriteBatch();

                var root = GetRoot(db, readOptions);
                var kvStore = new KVStore(db, MPT_FAMILY, readOptions, writeBatch);
                var mptTrie = new MPTTrie(root, kvStore);

                Blocks = new DataCache<UInt256, BlockState>(db, BLOCK_FAMILY, readOptions, writeBatch);
                Transactions = new DataCache<UInt256, TransactionState>(db, TX_FAMILY, readOptions, writeBatch);
                Accounts = new DataCache<UInt160, AccountState>(db, ACCOUNT_FAMILY, readOptions, writeBatch);
                UnspentCoins = new DataCache<UInt256, UnspentCoinState>(db, UNSPENT_COIN_FAMILY, readOptions, writeBatch);
                SpentCoins = new DataCache<UInt256, SpentCoinState>(db, SPENT_COIN_FAMILY, readOptions, writeBatch);
                Validators = new DataCache<ECPoint, ValidatorState>(db, VALIDATOR_FAMILY, readOptions, writeBatch);
                Assets = new DataCache<UInt256, AssetState>(db, ASSET_FAMILY, readOptions, writeBatch);
                Contracts = new DataCache<UInt160, ContractState>(db, CONTRACT_FAMILY, readOptions, writeBatch);
                Storages = new DataCache<StorageKey, StorageItem>(db, STORAGE_FAMILY, readOptions, writeBatch, mptTrie);
                StateRoots = new DataCache<UInt32Wrapper, StateRootState>(db, STATE_ROOT_FAMILY, readOptions, writeBatch);
                HeaderHashList = new DataCache<UInt32Wrapper, HeaderHashList>(db, HEADER_HASH_LIST_FAMILY, readOptions, writeBatch);
                ValidatorsCount = new MetaDataCache<ValidatorsCountState>(db, VALIDATORS_COUNT_KEY, readOptions, writeBatch);
                BlockHashIndex = new MetaDataCache<HashIndexState>(db, BLOCK_HASH_INDEX_KEY, readOptions, writeBatch);
                HeaderHashIndex = new MetaDataCache<HashIndexState>(db, HEADER_HASH_INDEX_KEY, readOptions, writeBatch);
                StateRootHashIndex = new MetaDataCache<RootHashIndex>(db, STATE_ROOT_HASH_INDEX_KEY, readOptions, writeBatch);
            }

            public override void Dispose()
            {
                snapshot.Dispose();
                writeBatch.Dispose();
            }

            public override void Commit()
            {
                base.Commit();
                db.Write(writeBatch);
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
            public override Neo.IO.Caching.DataCache<UInt32Wrapper, StateRootState> StateRoots { get; }
            public override Neo.IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList { get; }
            public override Neo.IO.Caching.MetaDataCache<ValidatorsCountState> ValidatorsCount { get; }
            public override Neo.IO.Caching.MetaDataCache<HashIndexState> BlockHashIndex { get; }
            public override Neo.IO.Caching.MetaDataCache<HashIndexState> HeaderHashIndex { get; }
            public override Neo.IO.Caching.MetaDataCache<RootHashIndex> StateRootHashIndex { get; }
        }
    }
}
