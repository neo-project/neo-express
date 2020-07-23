using System;
using Neo;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Seattle.Persistence;
using Neo.Plugins;
using NeoExpress.Abstractions.Models;

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
            foreach (var (key, value) in Blockchain.Singleton.View.Storages.Find())
            {
                if (key.Id == contract.Id)
                {
                    var storage = new JObject();
                    storage["key"] = key.Key.ToHexString();
                    storage["value"] = value.Value.ToHexString();
                    storage["constant"] = value.IsConstant;
                    storages.Add(storage);
                }
            }
            return storages;
        }

        [RpcMethod]
        public JObject? ExpressCreateCheckpoint(JArray @params)
        {
            string filename = @params[0].AsString();

            if (ProtocolSettings.Default.ValidatorsCount > 1)
            {
                throw new Exception("Checkpoint create is only supported on single node express instances");
            }

            if (Blockchain.Singleton.Store is RocksDbStore rocksDbStore)
            {
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
