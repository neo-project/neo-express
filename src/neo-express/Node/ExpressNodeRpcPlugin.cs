using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NeoExpress.Node
{
    internal class ExpressNodeRpcPlugin : Plugin, IRpcPlugin, IPersistencePlugin
    {
        private readonly Store store;
        private const byte APP_LOGS_PREFIX = 0x40;

        public ExpressNodeRpcPlugin(Store store)
        {
            this.store = store;
        }

        public override void Configure()
        {
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

        private JObject CreateContextResponse(ContractParametersContext context, Transaction? tx)
        {
            if (tx == null)
            {
                return new JObject();
            }

            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();

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
            var assetId = NodeUtility.GetAssetId(@params[0].AsString());
            var assetDescriptor = new AssetDescriptor(assetId);
            var quantity = BigDecimal.Parse(@params[1].AsString(), assetDescriptor.Decimals).ToFixed8();
            var sender = @params[2].AsString().ToScriptHash();
            var receiver = @params[3].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var tx = NodeUtility.MakeTransferTransaction(snapshot, ImmutableHashSet.Create(sender), receiver, assetId, quantity);
                var context = new ContractParametersContext(tx);

                return CreateContextResponse(context, tx);
            }
        }

        private JObject OnShowCoins(JArray @params)
        {
            var address = @params[0].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var coins = NodeUtility.GetCoins(snapshot, ImmutableHashSet.Create(address));

                return new JArray(coins.Select(c =>
                {
                    var j = new JObject();
                    j["state"] = (byte)c.State;
                    j["state-label"] = c.State.ToString();
                    j["reference"] = c.Reference.ToJson();
                    j["output"] = c.Output.ToJson(0);
                    return j;
                }));
            }
        }

        private JObject OnShowGas(JArray @params, bool showUnclaimed = false)
        {
            var address = @params[0].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var coins = NodeUtility.GetCoins(snapshot, ImmutableHashSet.Create(address));

                var unclaimedCoins = coins.Unclaimed(Blockchain.GoverningToken.Hash);
                var unspentCoins = coins.Unspent(Blockchain.GoverningToken.Hash);

                var unavailable = snapshot.CalculateBonus(
                    unspentCoins.Select(c => c.Reference),
                    snapshot.Height + 1);
                var available = snapshot.CalculateBonus(unclaimedCoins.Select(c => c.Reference));

                JObject json = new JObject();
                json["unavailable"] = (double)(decimal)unavailable;
                json["available"] = (double)(decimal)available;
                if (showUnclaimed)
                {
                    json["unclaimed"] = (double)(decimal)(available + unavailable);
                }
                return json;
            }
        }

        private JObject OnClaim(JArray @params)
        {
            UInt256 GetAssetId(string asset)
            {
                if (string.Compare("gas", asset, true) == 0)
                    return Blockchain.GoverningToken.Hash;

                return UInt256.Parse(asset);
            }

            var assetId = GetAssetId(@params[0].AsString());
            var address = @params[1].AsString().ToScriptHash();

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var tx = NodeUtility.MakeClaimTransaction(snapshot, address, assetId);
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

        private static JObject EngineToJson(ApplicationEngine engine)
        {
            var json = new JObject();
            json["state"] = (byte)engine.State;
            json["gas-consumed"] = engine.GasConsumed.ToString();
            json["result-stack"] = new JArray(engine.ResultStack.Select(item => item.ToParameter().ToJson()));
            return json;
        }

        public JObject OnDeployContract(JArray @params)
        {
            var contract = Newtonsoft.Json.Linq.JToken.Parse(@params[0].ToString());
            var address = @params[1].AsString().ToScriptHash();
            var addresses = ImmutableHashSet.Create(address);

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var (tx, engine) = NodeUtility.MakeDeploymentTransaction(snapshot, addresses, contract);
                var context = new ContractParametersContext(tx);
                var json = CreateContextResponse(context, tx);
                json["engine-state"] = EngineToJson(engine);
                return json;
            }
        }

        private static CoinReference CoinReferenceFromJson(JObject json)
        {
            var txid = UInt256.Parse(json["txid"].AsString());
            var vout = (ushort)json["vout"].AsNumber();

            return new CoinReference()
            {
                PrevHash = txid,
                PrevIndex = vout
            };
        }

        private static TransactionOutput TransactionOutputFromJson(JObject json)
        {
            var asset = UInt256.Parse(json["asset"].AsString());
            var value = (decimal)json["value"].AsNumber();
            var address = json["address"].AsString().ToScriptHash();

            return new TransactionOutput()
            {
                AssetId = asset,
                Value = Fixed8.FromDecimal(value),
                ScriptHash = address
            };
        }

        public JObject OnInvokeContract(JArray @params)
        {
            var scriptHash = UInt160.Parse(@params[0].AsString());
            var scriptParams = ((JArray)@params[1]).Select(ContractParameter.FromJson).ToArray();
            var address = @params[2] == JObject.Null ? null : @params[2].AsString().ToScriptHash();

            IEnumerable<CoinReference> inputs = Enumerable.Empty<CoinReference>();
            IEnumerable<TransactionOutput> outputs = Enumerable.Empty<TransactionOutput>();

            if (@params.Count == 5)
            {
                inputs = ((JArray)@params[3]).Select(CoinReferenceFromJson);
                outputs = ((JArray)@params[4]).Select(TransactionOutputFromJson);
            }

            var addresses = address == null ? ImmutableHashSet<UInt160>.Empty : ImmutableHashSet.Create(address);

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var (tx, engine) = NodeUtility.MakeInvocationTransaction(snapshot, addresses, scriptHash, scriptParams, inputs, outputs);
                var context = new ContractParametersContext(tx);
                var json = CreateContextResponse(context, tx);
                json["engine-state"] = EngineToJson(engine);
                return json;
            }
        }

        public JObject OnGetContractStorage(JArray @params)
        {
            var scriptHash = UInt160.Parse(@params[0].AsString());

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var storages = new JArray();
                foreach (var kvp in snapshot.Storages.Find())
                {
                    if (kvp.Key.ScriptHash == scriptHash)
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

        public JObject OnGetUnspents(JArray @params)
        {
            JObject GetBalance(IEnumerable<Coin> coins, UInt256 assetId, string symbol)
            {
                var unspents = new JArray();
                var total = Fixed8.Zero;
                foreach (var coin in coins.Where(c => c.Output.AssetId == assetId))
                {
                    var unspent = new JObject();
                    unspent["txid"] = coin.Reference.PrevHash.ToString().Substring(2);
                    unspent["n"] = coin.Reference.PrevIndex;
                    unspent["value"] = (double)(decimal)coin.Output.Value;

                    total += coin.Output.Value;
                    unspents.Add(unspent);
                }

                var balance = new JObject();
                balance["asset_hash"] = assetId.ToString().Substring(2);
                balance["asset_symbol"] = balance["asset"] = symbol;
                balance["amount"] = (double)(decimal)total;
                balance["unspent"] = unspents;

                return balance;
            }

            var address = @params[0].AsString().ToScriptHash();
            string[] nativeAssetNames = { "GAS", "NEO" };
            UInt256[] nativeAssetIds = { Blockchain.UtilityToken.Hash, Blockchain.GoverningToken.Hash };

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var coins = NodeUtility.GetCoins(snapshot, ImmutableHashSet.Create(address)).Unspent();

                var neoCoins = coins.Where(c => c.Output.AssetId == Blockchain.GoverningToken.Hash);
                var gasCoins = coins.Where(c => c.Output.AssetId == Blockchain.UtilityToken.Hash);

                JObject json = new JObject();
                json["address"] = address.ToAddress();
                json["balance"] = new JArray(
                    GetBalance(coins, Blockchain.GoverningToken.Hash, "NEO"),
                    GetBalance(coins, Blockchain.UtilityToken.Hash, "GAS"));
                return json;
            }
        }

        public JObject OnCheckpointCreate(JArray @params)
        {
            string filename = @params[0].AsString();

            if (ProtocolSettings.Default.StandbyValidators.Length > 1)
            {
                throw new Exception("Checkpoint create is only supported on single node express instances");
            }

            if (store is Persistence.RocksDbStore rocksDbStore)
            {
                var defaultAccount = System.RpcServer.Wallet.GetAccounts().Single(a => a.IsDefault);
                BlockchainOperations.CreateCheckpoint(
                    rocksDbStore,
                    filename,
                    ProtocolSettings.Default.Magic,
                    defaultAccount.ScriptHash.ToAddress());

                return filename;
            }
            else
            {
                throw new Exception("Checkpoint create is only supported for RocksDb storage implementation");
            }
        }

        public JObject OnGetApplicationLog(JArray @params)
        {
            var hash = UInt256.Parse(@params[0].AsString());
            var value = store.Get(APP_LOGS_PREFIX, hash.ToArray());

            if (value != null && value.Length > 0)
            {
                var json = Encoding.UTF8.GetString(value);
                return JObject.Parse(json);
            }

            throw new Exception("Unknown transaction");
        }

        JObject? IRpcPlugin.OnProcess(HttpContext context, string method, JArray @params)
        {
            switch (method)
            {
                case "getunspents":
                    return OnGetUnspents(@params);
                case "getapplicationlog":
                    return OnGetApplicationLog(@params);
                case "express-transfer":
                    return OnTransfer(@params);
                case "express-claim":
                    return OnClaim(@params);
                case "express-show-coins":
                    return OnShowCoins(@params);
                case "express-show-gas":
                case "getunclaimedgas":
                    return OnShowGas(@params);
                case "getunclaimed":
                    return OnShowGas(@params, true);
                case "express-submit-signatures":
                    return OnSubmitSignatures(@params);
                case "express-deploy-contract":
                    return OnDeployContract(@params);
                case "express-invoke-contract":
                    return OnInvokeContract(@params);
                case "express-get-contract-storage":
                    return OnGetContractStorage(@params);
                case "express-create-checkpoint":
                    return OnCheckpointCreate(@params);
            }

            return null;
        }

        void IRpcPlugin.PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        void IRpcPlugin.PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }

        private static JObject Convert(Blockchain.ApplicationExecuted appExec)
        {
            JObject json = new JObject();
            json["txid"] = appExec.Transaction.Hash.ToString();
            json["executions"] = appExec.ExecutionResults.Select(p =>
            {
                JObject execution = new JObject();
                execution["trigger"] = p.Trigger;
                execution["contract"] = p.ScriptHash.ToString();
                execution["vmstate"] = p.VMState;
                execution["gas_consumed"] = p.GasConsumed.ToString();
                try
                {
                    execution["stack"] = p.Stack.Select(q => q.ToParameter().ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    execution["stack"] = "error: recursive reference";
                }
                execution["notifications"] = p.Notifications.Select(q =>
                {
                    JObject notification = new JObject();
                    notification["contract"] = q.ScriptHash.ToString();
                    try
                    {
                        notification["state"] = q.State.ToParameter().ToJson();
                    }
                    catch (InvalidOperationException)
                    {
                        notification["state"] = "error: recursive reference";
                    }
                    return notification;
                }).ToArray();
                return execution;
            }).ToArray();
            return json;
        }

        void IPersistencePlugin.OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (var appExec in applicationExecutedList)
            {
                var json = Convert(appExec);
                var key = appExec.Transaction.Hash.ToArray();
                var value = Encoding.UTF8.GetBytes(json.ToString());
                store.Put(APP_LOGS_PREFIX, key, value);
            }
        }

        void IPersistencePlugin.OnCommit(Snapshot snapshot)
        {
        }

        bool IPersistencePlugin.ShouldThrowExceptionFromCommit(Exception ex) => false;
    }
}
