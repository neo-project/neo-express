using System;
using Neo;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Persistence;
using Neo.Plugins;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Persistence;

namespace NeoExpress.Neo3.Node
{
    class ExpressRpcServer
    {
        private readonly ExpressWalletAccount multiSigAccount;

        public ExpressRpcServer(ExpressWalletAccount multiSigAccount)
        {
            this.multiSigAccount = multiSigAccount;
        }

        [RpcMethod]
        private JObject? ExpressGetContractStorage(JArray @params)
        {
            var scriptHash = UInt160.Parse(@params[0].AsString());
            ContractState? contract = Blockchain.Singleton.View.Contracts.TryGet(scriptHash);
            if (contract == null) return null;

            var storages = new JArray();
            foreach (var kvp in Blockchain.Singleton.View.Storages.Find())
            {
                if (kvp.Key.Id == contract.Id)
                {
                    var storage = new JObject();
                    storage["key"] = kvp.Key.Key.ToHexString();
                    storage["value"] = kvp.Value.Value.ToHexString();
                    storage["constant"] = kvp.Value.IsConstant;
                    storages.Add(storage);
                }
            }
            return storages;
        }

        [RpcMethod]
        public JObject? ExpressCreateCheckpoint(JArray @params)
        {
            string filename = @params[0].AsString();

            if (ProtocolSettings.Default.StandbyValidators.Length > 1)
            {
                throw new Exception("Checkpoint create is only supported on single node express instances");
            }

            if (Blockchain.Singleton.Store is Persistence.RocksDbStore rocksDbStore)
            {
                var blockchainOperations = new BlockchainOperations();
                rocksDbStore.CreateCheckpoint(
                    filename,
                    ProtocolSettings.Default.Magic,
                    multiSigAccount.ScriptHash);

                return filename;
            }
            else
            {
                throw new Exception("Checkpoint create is only supported for RocksDb storage implementation");
            }
        }

    }
}
