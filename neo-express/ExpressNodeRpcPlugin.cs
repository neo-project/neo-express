using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Neo.Express
{
    internal class ExpressNodeRpcPlugin : Plugin, IRpcPlugin
    {
        public override void Configure()
        {
        }

        private static UInt256 GetAssetId(string asset)
        {
            if (string.Compare("neo", asset, true) == 0)
                return Blockchain.GoverningToken.Hash;

            if (string.Compare("gas", asset, true) == 0)
                return Blockchain.UtilityToken.Hash;

            return UInt256.Parse(asset);
        }

        private static JObject ToJson(ContractParametersContext context)
        {
            var json = new JObject();
            json["contract-context"] = context.ToJson();
            json["script-hashes"] = new JArray(context.ScriptHashes
                .Select(hash => new JString(hash.ToAddress())));
            json["hash-data"] = context.Verifiable.GetHashData().ToHexString();

            return json;
        }

        private JObject CreateContextResponse(ContractParametersContext context, Transaction tx)
        {
            if (tx == null)
            {
                return new JObject();
            }

            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();

                //ImmutableInterlocked.TryAdd(ref unconfirmed, tx.Hash, tx);
                System.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });

                JObject json = new JObject();
                json["txid"] = tx.Hash.ToString();
                return json;
            }
            else
            {
                return ToJson(context);
            }
        }

        private JObject OnTransfer(JArray @params)
        {
            var assetId = GetAssetId(@params[0].AsString());
            var assetDescriptor = new AssetDescriptor(assetId);
            var quantity = BigDecimal.Parse(@params[1].AsString(), assetDescriptor.Decimals).ToFixed8();
            var sender = @params[2].AsString().ToScriptHash();
            var receiver = @params[3].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var tx = NeoUtility.MakeTransferTransaction(snapshot, ImmutableHashSet.Create(sender), receiver, assetId, quantity);
                var context = new ContractParametersContext(tx);

                return CreateContextResponse(context, tx);
            }
        }

        private JObject OnShowCoins(JArray @params)
        {
            var address = @params[0].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var coins = NeoUtility.GetCoins(snapshot, ImmutableHashSet.Create(address));

                return new JArray(coins.Select(c =>
                {
                    var j = new JObject();
                    j["address"] = c.Address;
                    j["state"] = c.State.ToString();
                    j["reference"] = c.Reference.ToJson();
                    j["output"] = c.Output.ToJson(0);
                    return j;
                }));
            }
        }

        private JObject OnShowGas(JArray @params)
        {
            var address = @params[0].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var coins = NeoUtility.GetCoins(snapshot, ImmutableHashSet.Create(address));

                var unclaimedCoins = coins.Unclaimed(Blockchain.GoverningToken.Hash);
                var unspentCoins = coins.Unspent(Blockchain.GoverningToken.Hash);

                var unavailable = snapshot.CalculateBonus(
                    unspentCoins.Select(c => c.Reference),
                    snapshot.Height + 1);
                var available = snapshot.CalculateBonus(unclaimedCoins.Select(c => c.Reference));

                JObject json = new JObject();
                json["unavailable"] = (double)(decimal)unavailable;
                json["available"] = (double)(decimal)available;
                return json;
            }
        }

        private JObject OnClaim(JArray @params)
        {
            var assetId = GetAssetId(@params[0].AsString());
            var address = @params[1].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var tx = NeoUtility.MakeClaimTransaction(snapshot, address, assetId);
                var context = new ContractParametersContext(tx);

                return CreateContextResponse(context, tx);
            }
        }

        private JObject OnSubmitSignatures(JArray @params)
        {
            var context = ContractParametersContext.FromJson(@params[0]);
            var signatures = (JArray)@params[1];

            foreach (var signature in signatures)
            {
                var signatureData = signature["signature"].AsString().HexToBytes();
                var publicKeyData = signature["public-key"].AsString().HexToBytes();
                var contractScript = signature["contract"]["script"].AsString().HexToBytes();
                var parameters = ((JArray)signature["contract"]["parameters"])
                    .Select(j => Enum.Parse<ContractParameterType>(j.AsString()));

                var publicKey = ECPoint.FromBytes(publicKeyData, ECCurve.Secp256r1);
                var contract = Contract.Create(parameters.ToArray(), contractScript);
                if (!context.AddSignature(contract, publicKey, signatureData))
                {
                    throw new Exception($"AddSignature failed for {signature["public-key"].AsString()}");
                }

                if (context.Completed)
                    break;
            }

            if (context.Verifiable is Transaction tx)
            {
                return CreateContextResponse(context, tx);
            }
            else
            {
                throw new Exception("Only support to relay transaction");
            }
        }

        public JObject OnProcess(HttpContext context, string method, JArray @params)
        {
            switch (method)
            {
                case "express-transfer":
                    return OnTransfer(@params);
                case "express-claim":
                    return OnClaim(@params);
                case "express-show-coins":
                    return OnShowCoins(@params);
                case "express-show-gas":
                    return OnShowGas(@params);
                case "express-submit-signatures":
                    return OnSubmitSignatures(@params);
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
