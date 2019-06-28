using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.Express
{
    class ExpressNodeRpcPlugin : Plugin, IRpcPlugin
    {
        public override void Configure()
        {
        }

        static UInt160 GetAssetId(string asset)
        {
            if (string.Equals("neo", asset, StringComparison.OrdinalIgnoreCase))
            {
                return NativeContract.NEO.Hash;
            }

            if (string.Equals("gas", asset, StringComparison.OrdinalIgnoreCase))
            {
                return NativeContract.GAS.Hash;
            }

            return UInt160.Parse(asset);
        }

        static JObject ToJson(ContractParametersContext context)
        {
            var json = new JObject();
            json["contract-context"] = context.ToJson();
            json["script-hashes"] = new JArray(context.ScriptHashes
                .Select(hash => new JString(hash.ToAddress())));
            json["hash-data"] = context.Verifiable.GetHashData().ToHexString();

            return json;
        }

        private JObject OnTransfer(JArray @params)
        {
            var assetId = GetAssetId(@params[0].AsString());
            var assetDescriptor = new AssetDescriptor(assetId);
            var amount = BigDecimal.Parse(@params[1].AsString(), assetDescriptor.Decimals);
            var sender = @params[2].AsString().ToScriptHash();
            var receiver = @params[3].AsString().ToScriptHash();

            var tx = Wallet.MakeTransaction(new[] { sender },
                new[] { new TransferOutput {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = receiver } });
            var context = new ContractParametersContext(tx);

            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                System.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                return new JObject();
            }

            return ToJson(context);
        }

        public JObject OnProcess(HttpContext context, string method, JArray @params)
        {
            try
            {
                switch (method)
                {
                    case "express-tranfer":
                        return OnTransfer(@params);
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString(), LogLevel.Error);
            }

            return null;
        }

        public void PostProcess(HttpContext context, string method, JArray @params, JObject result)
        {
        }

        public void PreProcess(HttpContext context, string method, JArray @params)
        {
        }
    }
}
