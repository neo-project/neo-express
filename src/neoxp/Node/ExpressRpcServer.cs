using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress.Node
{
    class ExpressRpcServer
    {
        readonly NeoSystem neoSystem;
        readonly IExpressReadOnlyStore store;
        readonly ExpressWalletAccount multiSigAccount;
        readonly string cacheId;

        public ExpressRpcServer(NeoSystem neoSystem, IExpressReadOnlyStore store, ExpressWalletAccount multiSigAccount)
        {
            this.neoSystem = neoSystem;
            this.store = store;
            this.multiSigAccount = multiSigAccount;
            cacheId = DateTimeOffset.Now.Ticks.ToString();
        }

        [RpcMethod]
        public JObject ExpressGetPopulatedBlocks(JArray @params)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var height = NativeContract.Ledger.CurrentIndex(snapshot);

            var count = @params.Count >= 1 ? uint.Parse(@params[0].AsString()) : 20;
            count = count > 100 ? 100 : count;

            var start = @params.Count >= 2 ? uint.Parse(@params[1].AsString()) : height;
            start = start > height ? height : start;

            var populatedBlocks = new JArray();
            while (populatedBlocks.Count < count)
            {
                var hash = NativeContract.Ledger.GetBlockHash(snapshot, start);
                var trimmedBlock = NativeContract.Ledger.GetTrimmedBlock(snapshot, hash);

                if (trimmedBlock != null
                    && trimmedBlock.Hashes.Length > 1)
                {
                    populatedBlocks.Add(trimmedBlock.Index);
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
            return ExpressAppLogsPlugin.TryGetAppLog(store, hash) ?? throw new RpcException(-100, "Unknown transaction");
        }

        // TODO: should the event name comparison be case insensitive?
        // Native contracts use "Transfer" while neon preview 3 compiled contracts use "transfer"
        static bool IsNep17Transfer(NotificationRecord notification)
            => notification.InventoryType == InventoryType.TX
                && notification.State.Count == 3
                && (notification.EventName == "Transfer" || notification.EventName == "transfer");

        static IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNep17Transfers(IExpressReadOnlyStore store)
            => ExpressAppLogsPlugin
                .GetNotifications(store)
                .Where(t => IsNep17Transfer(t.notification));

        public static IEnumerable<Nep17Contract> GetNep17Contracts(NeoSystem neoSystem, IExpressReadOnlyStore store)
        {
            var scriptHashes = new HashSet<UInt160>();
            foreach (var (_, _, notification) in GetNep17Transfers(store))
            {
                scriptHashes.Add(notification.ScriptHash);
            }

            scriptHashes.Add(NativeContract.NEO.Hash);
            scriptHashes.Add(NativeContract.GAS.Hash);

            using var snapshot = neoSystem.GetSnapshot();
            foreach (var scriptHash in scriptHashes)
            {
                if (Nep17Contract.TryLoad(neoSystem.Settings, snapshot, scriptHash, out var contract))
                {
                    yield return contract;
                }
            }
        }

         UInt160 GetScriptHashFromParam(string addressOrScriptHash)
            => addressOrScriptHash.Length < 40
                ? addressOrScriptHash.ToScriptHash(neoSystem.Settings.AddressVersion)
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

        public static IEnumerable<(Nep17Contract contract, BigInteger balance, uint lastUpdatedBlock)> GetNep17Balances(NeoSystem neoSystem, IExpressReadOnlyStore store, UInt160 address)
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

            using var snapshot = neoSystem.GetSnapshot();
            foreach (var kvp in assets)
            {
                if (TryGetBalance(kvp.Key, out var balance)
                    && balance > BigInteger.Zero)
                {
                    var contract = Nep17Contract.TryLoad(neoSystem.Settings, snapshot, kvp.Key, out var _contract)
                        ? _contract : Nep17Contract.Unknown(kvp.Key);
                    yield return (contract, balance, kvp.Value);
                }
            }

            bool TryGetBalance(UInt160 asset, out BigInteger balance)
            {
                using var sb = new ScriptBuilder();
                sb.EmitDynamicCall(asset, "balanceOf", address.ToArray());

                using var engine = sb.Invoke(neoSystem.Settings, snapshot);
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
            var jsonContracts = new JArray();
            foreach (var contract in GetNep17Contracts(neoSystem, store))
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
            var balances = new JArray();
            foreach (var (contract, balance, lastUpdatedBlock) in GetNep17Balances(neoSystem, store, address))
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
                ["address"] = Neo.Wallets.Helper.ToAddress(address, neoSystem.Settings.AddressVersion),
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
                var addressVersion = neoSystem.Settings.AddressVersion;
                using var snapshot = neoSystem.GetSnapshot();
                foreach (var (blockIndex, txIndex, notification) in GetNep17Transfers(store))
                {
                    var header = NativeContract.Ledger.GetHeader(snapshot, blockIndex);
                    if (startTime <= header.Timestamp && header.Timestamp <= endTime)
                    {
                        var from = ToUInt160(notification.State[0]);
                        var to = ToUInt160(notification.State[1]);

                        if (address == from)
                        {
                            sent.Add(MakeTransferJson(addressVersion, blockIndex, txIndex, notification, header.Timestamp, to));
                        }

                        if (address == to)
                        {
                            received.Add(MakeTransferJson(addressVersion, blockIndex, txIndex, notification, header.Timestamp, from));
                        }
                    }
                }
            }

            return new JObject
            {
                ["address"] = Neo.Wallets.Helper.ToAddress(address, neoSystem.Settings.AddressVersion),
                ["sent"] = sent,
                ["received"] = received,
            };

            static JObject MakeTransferJson(byte addressVersion, uint blockIndex, ushort txIndex, NotificationRecord notification, ulong timestamp, UInt160 transferAddress)
                => new JObject
                {
                    ["timestamp"] = timestamp,
                    ["asset_hash"] = notification.ScriptHash.ToString(),
                    ["transfer_address"] = Neo.Wallets.Helper.ToAddress(transferAddress, addressVersion),
                    ["amount"] = notification.State[2].GetInteger().ToString(),
                    ["block_index"] = blockIndex,
                    ["transfer_notify_index"] = txIndex,
                    ["tx_hash"] = notification.InventoryHash.ToString(),
                };
        }

        [RpcMethod]
        public JObject? ExpressGetContractState(JArray @params)
        {
            using var snapshot = neoSystem.GetSnapshot();

            if (@params[0] is JNumber number)
            {
                var id = (int)number.AsNumber();
                foreach (var native in NativeContract.Contracts)
                {
                    if (id == native.Id)
                    {
                        var contract = NativeContract.ContractManagement.GetContract(snapshot, native.Hash);
                        return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
                    }
                }
            }

            var param = @params[0].AsString();

            if (UInt160.TryParse(param, out var scriptHash))
            {
                var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);
                return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
            }

            var contracts = new JArray();
            foreach (var contract in NativeContract.ContractManagement.ListContracts(snapshot))
            {
                if (param.Equals(contract.Manifest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    contracts.Add(contract.ToJson());
                }
            }
            return contracts;
        }

        [RpcMethod]
        public JObject? ExpressGetContractStorage(JArray @params)
        {
            var scriptHash = UInt160.Parse(@params[0].AsString());
            var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contract == null) return null;

            var storages = new JArray();
            byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
            using var snapshot = neoSystem.GetSnapshot();
            foreach (var (key, value) in snapshot.Find(prefix))
            {
                var storage = new JObject();
                storage["key"] = key.Key.ToHexString();
                storage["value"] = value.Value.ToHexString();
                storages.Add(storage);
            }
            return storages;
        }

        [RpcMethod]
        public JObject? ExpressListContracts(JArray @params)
        {
            var contracts = NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
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

            if (neoSystem.Settings.ValidatorsCount > 1)
            {
                throw new Exception("Checkpoint create is only supported on single node express instances");
            }

            if (store is RocksDbStore rocksDbStore)
            {
                rocksDbStore.CreateCheckpoint(filename, neoSystem.Settings, multiSigAccount.ScriptHash);

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
            var requests = new JArray();
            foreach (var (requestId, request) in NativeContract.Oracle.GetRequests(neoSystem.StoreView))
            {
                var json = new JObject();
                json["requestid"] = requestId;
                json["originaltxid"] = request.OriginalTxid.ToString();
                json["gasforresponse"] = request.GasForResponse;
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

            using var snapshot = neoSystem.GetSnapshot();
            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var oracleNodes = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Oracle, height);
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = OracleService.CreateResponseTx(snapshot, request, response, oracleNodes, neoSystem.Settings);
            return tx == null ? null : Convert.ToBase64String(tx.ToArray());
        }
    }
}
