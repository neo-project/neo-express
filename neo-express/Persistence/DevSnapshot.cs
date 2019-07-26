using Neo.Cryptography.ECC;
using Neo.IO.Caching;
using Neo.IO.Wrappers;
using Neo.Ledger;
using RocksDbSharp;

namespace Neo.Express.Persistence
{
    internal class DevSnapshot : Neo.Persistence.Snapshot
    {
        private readonly RocksDb db;
        private readonly RocksDbSharp.Snapshot snapshot;
        private readonly ReadOptions readOptions;
        private readonly WriteBatch writeBatch;

        public DevSnapshot(RocksDb db)
        {
            this.db = db;
            snapshot = db.CreateSnapshot();
            readOptions = new ReadOptions().SetSnapshot(snapshot);
            writeBatch = new WriteBatch();
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

        public override DataCache<UInt256, BlockState> Blocks =>
            DevStore.GetDataCache<UInt256, BlockState>(db, DevStore.BLOCK_FAMILY, readOptions, writeBatch);

        public override DataCache<UInt256, TransactionState> Transactions =>
            DevStore.GetDataCache<UInt256, TransactionState>(db, DevStore.TX_FAMILY, readOptions, writeBatch);

        public override DataCache<UInt160, AccountState> Accounts =>
            DevStore.GetDataCache<UInt160, AccountState>(db, DevStore.ACCOUNT_FAMILY, readOptions, writeBatch);

        public override DataCache<UInt256, UnspentCoinState> UnspentCoins =>
            DevStore.GetDataCache<UInt256, UnspentCoinState>(db, DevStore.UNSPENT_COIN_FAMILY, readOptions, writeBatch);

        public override DataCache<UInt256, SpentCoinState> SpentCoins =>
            DevStore.GetDataCache<UInt256, SpentCoinState>(db, DevStore.SPENT_COIN_FAMILY, readOptions, writeBatch);

        public override DataCache<ECPoint, ValidatorState> Validators =>
            DevStore.GetDataCache<ECPoint, ValidatorState>(db, DevStore.VALIDATOR_FAMILY, readOptions, writeBatch);

        public override DataCache<UInt256, AssetState> Assets =>
            DevStore.GetDataCache<UInt256, AssetState>(db, DevStore.ASSET_FAMILY, readOptions, writeBatch);

        public override DataCache<UInt160, ContractState> Contracts =>
            DevStore.GetDataCache<UInt160, ContractState>(db, DevStore.CONTRACT_FAMILY, readOptions, writeBatch);

        public override DataCache<StorageKey, StorageItem> Storages =>
            DevStore.GetDataCache<StorageKey, StorageItem>(db, DevStore.STORAGE_FAMILY, readOptions, writeBatch);

        public override DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList =>
            DevStore.GetDataCache<UInt32Wrapper, HeaderHashList>(db, DevStore.HEADER_HASH_LIST_FAMILY, readOptions, writeBatch);

        public override MetaDataCache<ValidatorsCountState> ValidatorsCount =>
            DevStore.GetMetaDataCache<ValidatorsCountState>(db, DevStore.VALIDATORS_COUNT_KEY, readOptions, writeBatch);

        public override MetaDataCache<HashIndexState> BlockHashIndex =>
            DevStore.GetMetaDataCache<HashIndexState>(db, DevStore.CURRENT_BLOCK_KEY, readOptions, writeBatch);

        public override MetaDataCache<HashIndexState> HeaderHashIndex =>
            DevStore.GetMetaDataCache<HashIndexState>(db, DevStore.CURRENT_HEADER_KEY, readOptions, writeBatch);
    }
}
