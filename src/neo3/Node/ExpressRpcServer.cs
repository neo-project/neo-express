using Neo;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Plugins;

namespace NeoExpress.Neo3.Node
{
    class ExpressRpcServer
    {
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
    }
}
