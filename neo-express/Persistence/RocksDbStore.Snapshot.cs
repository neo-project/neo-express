using Neo.Cryptography.ECC;
using Neo.IO.Caching;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;

namespace Neo.Express.Persistence
{
    internal partial class RocksDbStore
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

                Blocks = GetDataCache<UInt256, BlockState>(
                    db, BLOCK_FAMILY, readOptions, writeBatch);
                Transactions = GetDataCache<UInt256, TransactionState>(
                    db, TX_FAMILY, readOptions, writeBatch);
                Accounts = GetDataCache<UInt160, AccountState>(
                    db, ACCOUNT_FAMILY, readOptions, writeBatch);
                UnspentCoins = GetDataCache<UInt256, UnspentCoinState>(
                    db, UNSPENT_COIN_FAMILY, readOptions, writeBatch);
                SpentCoins = GetDataCache<UInt256, SpentCoinState>(
                    db, SPENT_COIN_FAMILY, readOptions, writeBatch);
                Validators = GetDataCache<ECPoint, ValidatorState>(
                    db, VALIDATOR_FAMILY, readOptions, writeBatch);
                Assets = GetDataCache<UInt256, AssetState>(
                    db, ASSET_FAMILY, readOptions, writeBatch);
                Contracts = GetDataCache<UInt160, ContractState>(
                    db, CONTRACT_FAMILY, readOptions, writeBatch);
                Storages = GetDataCache<StorageKey, StorageItem>(
                    db, STORAGE_FAMILY, readOptions, writeBatch);
                HeaderHashList = GetDataCache<UInt32Wrapper, HeaderHashList>(
                    db, HEADER_HASH_LIST_FAMILY, readOptions, writeBatch);
                ValidatorsCount = GetMetaDataCache<ValidatorsCountState>(
                    db, VALIDATORS_COUNT_KEY, readOptions, writeBatch);
                BlockHashIndex = GetMetaDataCache<HashIndexState>(
                    db, CURRENT_BLOCK_KEY, readOptions, writeBatch);
                HeaderHashIndex = GetMetaDataCache<HashIndexState>(
                    db, CURRENT_HEADER_KEY, readOptions, writeBatch);
            }

            public override void Dispose()
            {
                snapshot.Dispose();
            }

            public override void Commit()
            {
                base.Commit();
                db.Write(writeBatch);
            }

            public override IO.Caching.DataCache<UInt256, BlockState> Blocks { get; }
            public override IO.Caching.DataCache<UInt256, TransactionState> Transactions { get; }
            public override IO.Caching.DataCache<UInt160, AccountState> Accounts { get; }
            public override IO.Caching.DataCache<UInt256, UnspentCoinState> UnspentCoins { get; }
            public override IO.Caching.DataCache<UInt256, SpentCoinState> SpentCoins { get; }
            public override IO.Caching.DataCache<ECPoint, ValidatorState> Validators { get; }
            public override IO.Caching.DataCache<UInt256, AssetState> Assets { get; }
            public override IO.Caching.DataCache<UInt160, ContractState> Contracts { get; }
            public override IO.Caching.DataCache<StorageKey, StorageItem> Storages { get; }
            public override IO.Caching.DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList { get; }
            public override IO.Caching.MetaDataCache<ValidatorsCountState> ValidatorsCount { get; }
            public override IO.Caching.MetaDataCache<HashIndexState> BlockHashIndex { get; }
            public override IO.Caching.MetaDataCache<HashIndexState> HeaderHashIndex { get; }
        }
    }
}
