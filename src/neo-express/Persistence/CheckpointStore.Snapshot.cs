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
                Blocks = store._blocks.GetSnapshot();
                Transactions = store._transactions.GetSnapshot();
                Accounts = store._accounts.GetSnapshot();
                UnspentCoins = store._unspentCoins.GetSnapshot();
                SpentCoins = store._spentCoins.GetSnapshot();
                Validators = store._validators.GetSnapshot();
                Assets = store._assets.GetSnapshot();
                Contracts = store._contracts.GetSnapshot();
                Storages = store._storages.GetSnapshot();
                HeaderHashList = store._headerHashList.GetSnapshot();

                ValidatorsCount = store._validatorsCount.GetSnapshot();
                BlockHashIndex = store._blockHashIndex.GetSnapshot();
                HeaderHashIndex = store._headerHashIndex.GetSnapshot();
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
