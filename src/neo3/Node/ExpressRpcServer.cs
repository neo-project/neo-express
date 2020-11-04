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

namespace NeoExpress.Neo3.Node
{
    internal class ExpressRpcServer
    {
        readonly ExpressWalletAccount multiSigAccount;
        readonly DataCache<UInt256, TrimmedBlock> blocksCache;
        public ExpressRpcServer(ExpressWalletAccount multiSigAccount)
        {
            this.multiSigAccount = multiSigAccount;
            this.blocksCache = Blockchain.Singleton.View.Blocks;
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
                var blockState = blocksCache.TryGet(hash);
                if (blockState != null
                    && blockState.Hashes.Length > 0)
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
            return populatedBlocks;
        }
        
        [RpcMethod]
        public JObject GetApplicationLog(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            return ExpressAppLogsPlugin.TryGetAppLog(Blockchain.Singleton.Store, hash) ?? throw new RpcException(-100, "Unknown transaction");
        }

        // TODO: should the event name comparison be case insensitive? 
        // Native contracts use "Transfer" while neon preview 3 compiled contracts use "transfer"
        static bool IsNep5Transfer(NotificationRecord notification)
            => notification.InventoryType == InventoryType.TX
                && notification.State.Count == 3
                && (notification.EventName == "Transfer" || notification.EventName == "transfer");

        static IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNep5Transfers(IReadOnlyStore store)
        {
            return ExpressAppLogsPlugin
                .GetNotifications(store)
                .Where(t => IsNep5Transfer(t.notification));
        }

        public static IEnumerable<Nep5Contract> GetNep5Contracts(IReadOnlyStore store)
        {
            var scriptHashes = new HashSet<UInt160>();
            foreach (var (_, _, notification) in GetNep5Transfers(store))
            {
                scriptHashes.Add(notification.ScriptHash);
            }

            foreach (var scriptHash in scriptHashes)
            {
                if (Nep5Contract.TryLoad(store, scriptHash, out var contract))
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

        public static IEnumerable<(Nep5Contract contract, BigInteger balance, uint lastUpdatedBlock)> GetNep5Balances(IReadOnlyStore store, UInt160 address)
        {
            var accounts = new Dictionary<UInt160, uint>();

            foreach (var (blockIndex, _, notification) in GetNep5Transfers(store))
            {
                var from = ToUInt160(notification.State[0]);
                var to = ToUInt160(notification.State[1]);
                if (from == address || to == address)
                {
                    accounts[notification.ScriptHash] = blockIndex;
                }
            }

            foreach (var kvp in accounts)
            {
                if (TryGetBalance(kvp.Key, out var balance))
                {
                    var contract = Nep5Contract.TryLoad(store, kvp.Key, out var _contract)
                        ? _contract : Nep5Contract.Unknown(kvp.Key);
                    yield return (contract, balance, kvp.Value);
                }
            }

            bool TryGetBalance(UInt160 asset, out BigInteger balance)
            {
                using var sb = new ScriptBuilder();
                sb.EmitAppCall(asset, "balanceOf", address.ToArray());

                using var engine = ApplicationEngine.Run(sb.ToArray(), new ReadOnlyView(store));
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
        public JObject ExpressGetNep5Contracts(JArray _)
        {
            using var snapshot = Blockchain.Singleton.Store.GetSnapshot();
            var jsonContracts = new JArray();
            foreach (var contract in GetNep5Contracts(snapshot))
            {
                jsonContracts.Add(contract.ToJson());
            }
            return jsonContracts;
        }

        [RpcMethod]
        public JObject GetNep5Balances(JArray @params)
        {
            var address = GetScriptHashFromParam(@params[0].AsString());
            using var snapshot = Blockchain.Singleton.Store.GetSnapshot();
            var balances = new JArray();
            foreach (var (contract, balance, lastUpdatedBlock) in GetNep5Balances(snapshot, address))
            {
                balances.Add(new JObject()
                {
                    ["asset_hash"] = contract.ScriptHash.ToString(),
                    ["amount"] = balance.ToString(),
                    ["last_updated_block"] = lastUpdatedBlock,
                });
            }
            return new JObject
            {
                ["address"] = Neo.Wallets.Helper.ToAddress(address),
                ["balance"] = balances,
            };
        }

        [RpcMethod]
        public JObject GetNep5Transfers(JArray @params)
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
                foreach (var (blockIndex, txIndex, notification) in GetNep5Transfers(snapshot))
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
            Neo.Ledger.ContractState? contract = Blockchain.Singleton.View.Contracts.TryGet(scriptHash);
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
            var contracts = Blockchain.Singleton.View.Contracts.Find().OrderBy(t => t.Value.Id);

            var json = new JArray();
            foreach (var (key, value) in contracts)
            {
                json.Add(value.Manifest.ToJson());
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
    }
}
