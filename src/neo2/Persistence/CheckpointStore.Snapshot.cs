﻿using System;
using System.Collections.Generic;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Wrappers;
using Neo.Ledger;
using Neo.Trie;
using Neo.Trie.MPT;

namespace NeoExpress.Neo2.Persistence
{
    internal partial class CheckpointStore
    {

        private class Snapshot : Neo.Persistence.Snapshot
        {
            public Snapshot(CheckpointStore store)
            {
                var kvStore = store.kvTracker.GetSnapshot();

                Blocks = store.blocks.GetSnapshot();
                Transactions = store.transactions.GetSnapshot();
                Accounts = store.accounts.GetSnapshot();
                UnspentCoins = store.unspentCoins.GetSnapshot();
                SpentCoins = store.spentCoins.GetSnapshot();
                Validators = store.validators.GetSnapshot();
                Assets = store.assets.GetSnapshot();
                Contracts = store.contracts.GetSnapshot();
                Storages = store.storages.GetSnapshot(kvStore);
                StateRoots = store.stateRoots.GetSnapshot();
                HeaderHashList = store.headerHashList.GetSnapshot();

                ValidatorsCount = store.validatorsCount.GetSnapshot();
                BlockHashIndex = store.blockHashIndex.GetSnapshot();
                HeaderHashIndex = store.headerHashIndex.GetSnapshot();
                StateRootHashIndex = store.stateRootHashIndex.GetSnapshot();
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
