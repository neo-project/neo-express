using System;
using Neo;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.BlockchainToolkit.Persistence;
using Neo.Plugins;
using NeoExpress.Abstractions.Models;
using System.Linq;
using Neo.IO;
using System.Text;
using Neo.Network.P2P.Payloads;
using System.Collections.Generic;
using NeoExpress.Neo3.Models;
using System.Numerics;
using Neo.VM;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.Persistence;
using Neo.IO.Caching;
using Neo.SmartContract.Native;

namespace NeoExpress.Neo3.Node
{
    class ExpressRpcServer
    {
        readonly ExpressWalletAccount multiSigAccount;
        readonly string cacheId;

        public ExpressRpcServer(ExpressWalletAccount multiSigAccount)
        {
            this.multiSigAccount = multiSigAccount;
            cacheId = DateTimeOffset.Now.Ticks.ToString();
        }

        [RpcMethod]
        public JObject ExpressGetPopulatedBlocks(JArray @params)
        {
            var height = Blockchain.Singleton.Height;

            var count = @params.Count >= 1 ? uint.Parse(@params[0].AsString()) : 20;
            count = count > 100 ? 100 : count;

            var start = @params.Count >= 2 ? uint.Parse(@params[1].AsString()) : height;
            start = start > height ? height : start;

            var populatedBlocks = new JArray();
            while (populatedBlocks.Count < count)
            {
                var hash = Blockchain.Singleton.GetBlockHash(start);
                var blockState = Blockchain.Singleton.View.Blocks.TryGet(hash);

                if (blockState != null
                    && blockState.Hashes.Length > 1)
                {
                    populatedBlocks.Add(blockState.Index);
                }

                if (start == 0)
                {
                    break;
                }
                else
                {
                    start--;
                }
            }

            var response = new JObject();
            response["cacheId"] = cacheId;
            response["blocks"] = populatedBlocks;
            return response;
        }

        [RpcMethod]
        public JObject GetApplicationLog(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            return ExpressAppLogsPlugin.TryGetAppLog(Blockchain.Singleton.Store, hash) ?? throw new RpcException(-100, "Unknown transaction");
        }

        // TODO: should the event name comparison be case insensitive?
        // Native contracts use "Transfer" while neon preview 3 compiled contracts use "transfer"
        static bool IsNep17Transfer(NotificationRecord notification)
            => notification.InventoryType == InventoryType.TX
                && notification.State.Count == 3
                && (notification.EventName == "Transfer" || notification.EventName == "transfer");

        static IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNep17Transfers(IReadOnlyStore store) 
            => ExpressAppLogsPlugin
                .GetNotifications(store)
                .Where(t => IsNep17Transfer(t.notification));

        public static IEnumerable<Nep17Contract> GetNep17Contracts(IReadOnlyStore store)
        {
            var scriptHashes = new HashSet<UInt160>();
            foreach (var (_, _, notification) in GetNep17Transfers(store))
            {
                scriptHashes.Add(notification.ScriptHash);
            }

            scriptHashes.Add(NativeContract.NEO.Hash);
            scriptHashes.Add(NativeContract.GAS.Hash);

            var snapshot = new ReadOnlyView(store);
            foreach (var scriptHash in scriptHashes)
            {
                if (Nep17Contract.TryLoad(snapshot, scriptHash, out var contract))
                {
                    yield return contract;
                }
            }
        }

        static UInt160 GetScriptHashFromParam(string addressOrScriptHash)
            => addressOrScriptHash.Length < 40
                ? addressOrScriptHash.ToScriptHash()
                : UInt160.Parse(addressOrScriptHash);

        static UInt160 ToUInt160(Neo.VM.Types.StackItem item)
        {
            if (item.IsNull) return UInt160.Zero;

            var bytes = item.GetSpan();
            return bytes.Length == 0
                ? UInt160.Zero
                : bytes.Length == 20
                    ? new UInt160(bytes)
                    : throw new ArgumentException("invalid UInt160", nameof(item));
        }

        public static IEnumerable<(Nep17Contract contract, BigInteger balance, uint lastUpdatedBlock)> GetNep17Balances(IReadOnlyStore store, UInt160 address)
        {
            // assets key is the script hash of the asset contract
            // assets value is the last updated block of the assoicated asset for address
            var assets = new Dictionary<UInt160, uint>();

            foreach (var (blockIndex, _, notification) in GetNep17Transfers(store))
            {
                var from = ToUInt160(notification.State[0]);
                var to = ToUInt160(notification.State[1]);
                if (from == address || to == address)
                {
                    assets[notification.ScriptHash] = blockIndex;
                }
            }

            if (!assets.ContainsKey(NativeContract.NEO.Hash))
            {
                assets[NativeContract.NEO.Hash] = 0;
            }

            if (!assets.ContainsKey(NativeContract.GAS.Hash))
            {
                assets[NativeContract.GAS.Hash] = 0;
            }

            var snapshot = new ReadOnlyView(store);
            foreach (var kvp in assets)
            {
                if (TryGetBalance(kvp.Key, out var balance)
                    && balance > BigInteger.Zero)
                {
                    var contract = Nep17Contract.TryLoad(snapshot, kvp.Key, out var _contract)
                        ? _contract : Nep17Contract.Unknown(kvp.Key);
                    yield return (contract, balance, kvp.Value);
                }
            }

            bool TryGetBalance(UInt160 asset, out BigInteger balance)
            {
                using var sb = new ScriptBuilder();
                sb.EmitAppCall(asset, "balanceOf", address.ToArray());

                using var engine = ApplicationEngine.Run(sb.ToArray(), snapshot);
                if (!engine.State.HasFlag(VMState.FAULT) && engine.ResultStack.Count >= 1)
                {
                    balance = engine.ResultStack.Pop<Neo.VM.Types.Integer>().GetInteger();
                    return true;
                }

                balance = default;
                return false;
            }
        }

        [RpcMethod]
        public JObject ExpressGetNep17Contracts(JArray _)
        {
            using var snapshot = Blockchain.Singleton.Store.GetSnapshot();
            var jsonContracts = new JArray();
            foreach (var contract in GetNep17Contracts(snapshot))
            {
                var jsonContract = new JObject();
                jsonContracts.Add(contract.ToJson());
            }
            return jsonContracts;
        }

        [RpcMethod]
        public JObject GetNep17Balances(JArray @params)
        {
            var address = GetScriptHashFromParam(@params[0].AsString());
            using var snapshot = Blockchain.Singleton.Store.GetSnapshot();
            var balances = new JArray();
            foreach (var (contract, balance, lastUpdatedBlock) in GetNep17Balances(snapshot, address))
            {
                balances.Add(new JObject()
                {
                    ["assethash"] = contract.ScriptHash.ToString(),
                    ["amount"] = balance.ToString(),
                    ["lastupdatedblock"] = lastUpdatedBlock,
                });
            }
            return new JObject
            {
                ["address"] = Neo.Wallets.Helper.ToAddress(address),
                ["balance"] = balances,
            };
        }

        [RpcMethod]
        public JObject GetNep17Transfers(JArray @params)
        {
            var address = GetScriptHashFromParam(@params[0].AsString());

            // If start time not present, default to 1 week of history.
            uint startTime = @params.Count > 1 ? (uint)@params[1].AsNumber() :
                (DateTime.UtcNow - TimeSpan.FromDays(7)).ToTimestamp();
            uint endTime = @params.Count > 2 ? (uint)@params[2].AsNumber() : DateTime.UtcNow.ToTimestamp();

            if (endTime < startTime) throw new RpcException(-32602, "Invalid params");

            var sent = new JArray();
            var received = new JArray();

            {
                using var snapshot = Blockchain.Singleton.Store.GetSnapshot();
                foreach (var (blockIndex, txIndex, notification) in GetNep17Transfers(snapshot))
                {
                    var header = Blockchain.Singleton.GetHeader(blockIndex);
                    if (startTime <= header.Timestamp && header.Timestamp <= endTime)
                    {
                        var from = ToUInt160(notification.State[0]);
                        var to = ToUInt160(notification.State[1]);

                        if (address == from)
                        {
                            sent.Add(MakeTransferJson(blockIndex, txIndex, notification, header.Timestamp, to));
                        }

                        if (address == to)
                        {
                            received.Add(MakeTransferJson(blockIndex, txIndex, notification, header.Timestamp, from));
                        }
                    }
                }
            }

            return new JObject
            {
                ["address"] = Neo.Wallets.Helper.ToAddress(address),
                ["sent"] = sent,
                ["received"] = received,
            };

            static JObject MakeTransferJson(uint blockIndex, ushort txIndex, NotificationRecord notification, ulong timestamp, UInt160 transferAddress)
                => new JObject
                {
                    ["timestamp"] = timestamp,
                    ["asset_hash"] = notification.ScriptHash.ToString(),
                    ["transfer_address"] = Neo.Wallets.Helper.ToAddress(transferAddress),
                    ["amount"] = notification.State[2].GetInteger().ToString(),
                    ["block_index"] = blockIndex,
                    ["transfer_notify_index"] = txIndex,
                    ["tx_hash"] = notification.InventoryHash.ToString(),
                };
        }

        [RpcMethod]
        public JObject? ExpressGetContractStorage(JArray @params)
        {
            var scriptHash = UInt160.Parse(@params[0].AsString());
            var contract = NativeContract.Management.GetContract(Blockchain.Singleton.View, scriptHash);
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
        public JObject? ExpressListContracts(JArray @params)
        {
            var contracts = NodeUtility.ListContracts(Blockchain.Singleton.View)
                .OrderBy(c => c.Id);

            var json = new JArray();
            foreach (var contract in contracts)
            {
                var jsonContract = new JObject();
                jsonContract["hash"] = contract.Hash.ToString();
                jsonContract["manifest"] = contract.Manifest.ToJson();
                json.Add(jsonContract);
            }
            return json;
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

        [RpcMethod]
        public JObject? ExpressListOracleRequests(JArray _)
        {
            using var snapshot = Blockchain.Singleton.GetSnapshot();

            var requests = new JArray();
            foreach (var (id, request) in NativeContract.Oracle.GetRequests(snapshot))
            {
                var json = new JObject();
                json["requestid"] = id.ToString();
                json["originaltxid"] = request.OriginalTxid.ToString();
                json["gasforresponse"] = request.GasForResponse.ToString();
                json["url"] = request.Url;
                json["filter"] = request.Filter;
                json["callbackcontract"] = request.CallbackContract.ToString();
                json["callbackmethod"] = request.CallbackMethod;
                json["userdata"] = Convert.ToBase64String(request.UserData);
                requests.Add(json);
            }
            return requests;
        }

        [RpcMethod]
        public JObject? ExpressCreateOracleResponseTx(JArray @params)
        {
            var jsonResponse = @params[0];
            var response = new OracleResponse
            {
                Id = (ulong)jsonResponse["id"].AsNumber(),
                Code = (OracleResponseCode)jsonResponse["code"].AsNumber(),
                Result = Convert.FromBase64String(jsonResponse["result"].AsString())
            };

            using var snapshot = Blockchain.Singleton.GetSnapshot();
            var tx = ExpressOracle.CreateResponseTx(snapshot, response);
            return tx == null ? null : Convert.ToBase64String(tx.ToArray());
        }
    }
}
