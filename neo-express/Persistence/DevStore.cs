using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Wrappers;
using Neo.Ledger;
using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Text;

namespace Neo.Express.Persistence
{
    internal class DevStore : Store, IDisposable
    {
        public const string BLOCK_FAMILY = "data:block";
        public const string TX_FAMILY = "data:transaction";
        public const string ACCOUNT_FAMILY = "st:account";
        public const string ASSET_FAMILY = "st:asset";
        public const string CONTRACT_FAMILY = "st:contract";
        public const string HEADER_HASH_LIST_FAMILY = "ix:header-hash-list";
        public const string SPENT_COIN_FAMILY = "st:spent-coin";
        public const string STORAGE_FAMILY = "st:storage";
        public const string UNSPENT_COIN_FAMILY = "st:coin";
        public const string VALIDATOR_FAMILY = "st:validator";
        public const string METADATA_FAMILY = "metadata";

        public const byte VALIDATORS_COUNT_KEY = 0x90;
        public const byte CURRENT_BLOCK_KEY = 0xc0;
        public const byte CURRENT_HEADER_KEY = 0xc1;

        public static DevDataCache<TKey, TValue> GetDataCache<TKey, TValue>(
            RocksDb db, string familyName, ReadOptions readOptions = null, WriteBatch writeBatch = null)
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            var columnFamily = db.GetColumnFamily(familyName);
            return new DevDataCache<TKey, TValue>(db, columnFamily, readOptions, writeBatch);
        }

        public static DevMetaDataCache<T> GetMetaDataCache<T>(
            RocksDb db, byte key, ReadOptions readOptions = null, WriteBatch writeBatch = null)
            where T : class, ICloneable<T>, ISerializable, new()
        {
            var columnFamily = db.GetColumnFamily(METADATA_FAMILY);
            var keyArray = new byte[1] { key };
            return new DevMetaDataCache<T>(db, keyArray, columnFamily, readOptions, writeBatch);
        }

        private readonly RocksDb db;

        public DevStore(string path)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            var columnFamilies = new ColumnFamilies {
                { BLOCK_FAMILY, new ColumnFamilyOptions() },
                { TX_FAMILY, new ColumnFamilyOptions() },
                { ACCOUNT_FAMILY, new ColumnFamilyOptions() },
                { UNSPENT_COIN_FAMILY, new ColumnFamilyOptions() },
                { SPENT_COIN_FAMILY, new ColumnFamilyOptions() },
                { VALIDATOR_FAMILY, new ColumnFamilyOptions() },
                { ASSET_FAMILY, new ColumnFamilyOptions() },
                { CONTRACT_FAMILY, new ColumnFamilyOptions() },
                { STORAGE_FAMILY, new ColumnFamilyOptions() },
                { HEADER_HASH_LIST_FAMILY, new ColumnFamilyOptions() },
                { METADATA_FAMILY, new ColumnFamilyOptions() }};

            db = RocksDb.Open(options, path, columnFamilies);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public override DataCache<UInt160, AccountState> GetAccounts() => 
            GetDataCache<UInt160, AccountState>(db, ACCOUNT_FAMILY);

        public override DataCache<UInt256, AssetState> GetAssets() =>
            GetDataCache<UInt256, AssetState>(db, ASSET_FAMILY);

        public override DataCache<UInt256, BlockState> GetBlocks() =>
            GetDataCache<UInt256, BlockState>(db, BLOCK_FAMILY);

        public override DataCache<UInt160, ContractState> GetContracts() =>
            GetDataCache<UInt160, ContractState>(db, CONTRACT_FAMILY);

        public override DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList() =>
            GetDataCache<UInt32Wrapper, HeaderHashList>(db, HEADER_HASH_LIST_FAMILY);

        public override DataCache<UInt256, SpentCoinState> GetSpentCoins() =>
            GetDataCache<UInt256, SpentCoinState>(db, SPENT_COIN_FAMILY);

        public override DataCache<StorageKey, StorageItem> GetStorages() =>
            GetDataCache<StorageKey, StorageItem>(db, STORAGE_FAMILY);

        public override DataCache<UInt256, TransactionState> GetTransactions() =>
            GetDataCache<UInt256, TransactionState>(db, TX_FAMILY);

        public override DataCache<UInt256, UnspentCoinState> GetUnspentCoins() =>
            GetDataCache<UInt256, UnspentCoinState>(db, UNSPENT_COIN_FAMILY);

        public override DataCache<ECPoint, ValidatorState> GetValidators() =>
            GetDataCache<ECPoint, ValidatorState>(db, VALIDATOR_FAMILY);

        public override MetaDataCache<HashIndexState> GetBlockHashIndex() =>
            GetMetaDataCache<HashIndexState>(db, CURRENT_BLOCK_KEY);

        public override MetaDataCache<HashIndexState> GetHeaderHashIndex() =>
            GetMetaDataCache<HashIndexState>(db, CURRENT_HEADER_KEY);

        public override MetaDataCache<ValidatorsCountState> GetValidatorsCount() =>
            GetMetaDataCache<ValidatorsCountState>(db, VALIDATORS_COUNT_KEY);

        public override Neo.Persistence.Snapshot GetSnapshot() =>
            new DevSnapshot(db);

        private static byte[] GetKey(byte prefix, byte[] key)
        {
            var tempKey = new byte[key.Length + 1];
            tempKey[0] = prefix;
            key.CopyTo(tempKey, 1);
            return tempKey;
        }

        public override byte[] Get(byte prefix, byte[] key)
        {
            return db.Get(GetKey(prefix, key));
        }

        public override void Put(byte prefix, byte[] key, byte[] value)
        {
            db.Put(GetKey(prefix, key), value);
        }

        public override void PutSync(byte prefix, byte[] key, byte[] value)
        {
            db.Put(GetKey(prefix, key), value, 
                writeOptions: new WriteOptions().SetSync(true));
        }
    }
}
