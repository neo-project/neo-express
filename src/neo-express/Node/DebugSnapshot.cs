using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Caching;
using Neo.IO.Wrappers;
using Neo.Ledger;
using Neo.VM;
using System;
using System.Linq;

namespace NeoExpress.Node
{
    internal class DebugSnapshot : Neo.Persistence.Snapshot, IScriptTable, IDisposable
    {
        private readonly NeoDebug.Models.Contract contract;
        private readonly Neo.Persistence.Snapshot snapshot;

        public DebugSnapshot(NeoDebug.Models.Contract contract, Neo.Persistence.Snapshot snapshot)
        {
            this.contract = contract;
            this.snapshot = snapshot;
        }

        byte[] IScriptTable.GetScript(byte[] script_hash)
        {
            if (script_hash.SequenceEqual(contract.ScriptHash))
            {
                return contract.Script;
            }

            return Contracts[new UInt160(script_hash)].Script;
        }

        void IDisposable.Dispose()
        {
            snapshot.Dispose();
        }

        public override DataCache<UInt256, BlockState> Blocks => snapshot.Blocks;

        public override DataCache<UInt256, TransactionState> Transactions => snapshot.Transactions;

        public override DataCache<UInt160, AccountState> Accounts => snapshot.Accounts;

        public override DataCache<UInt256, UnspentCoinState> UnspentCoins => snapshot.UnspentCoins;

        public override DataCache<UInt256, SpentCoinState> SpentCoins => snapshot.SpentCoins;

        public override DataCache<ECPoint, ValidatorState> Validators => snapshot.Validators;

        public override DataCache<UInt256, AssetState> Assets => snapshot.Assets;

        public override DataCache<UInt160, ContractState> Contracts => snapshot.Contracts;

        public override DataCache<StorageKey, StorageItem> Storages => snapshot.Storages;

        public override DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList => snapshot.HeaderHashList;

        public override MetaDataCache<ValidatorsCountState> ValidatorsCount => snapshot.ValidatorsCount;

        public override MetaDataCache<HashIndexState> BlockHashIndex => snapshot.BlockHashIndex;

        public override MetaDataCache<HashIndexState> HeaderHashIndex => snapshot.HeaderHashIndex;
    }
}
