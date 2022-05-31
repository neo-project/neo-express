using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using Neo;
using Neo.IO;
using Neo.IO.Json;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Commands;
using NeoExpress.Models;
using OracleResponse = Neo.Network.P2P.Payloads.OracleResponse;
using OracleResponseCode = Neo.Network.P2P.Payloads.OracleResponseCode;
using RpcClient = Neo.Network.RPC.RpcClient;

namespace NeoExpress.Node
{
    // Note, I need to think thru an alternative way to unify online/offline behavior
    // something more productive than "implement custom behavior twice"

    partial class ExpressSystem
    {
        readonly CancellationTokenSource shutdownTokenSource = new();
        readonly string cacheId = DateTimeOffset.Now.Ticks.ToString();

        [RpcMethod]
        public JObject ExpressShutdown(JArray @params)
        {
            const int SHUTDOWN_TIME = 2;

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var response = new JObject();
            response["process-id"] = proc.Id;

            Neo.Utility.Log(nameof(ExpressSystem), Neo.LogLevel.Info, $"ExpressShutdown requested. Shutting down in {SHUTDOWN_TIME} seconds");
            shutdownTokenSource.CancelAfter(TimeSpan.FromSeconds(SHUTDOWN_TIME));
            return response;
        }

        [RpcMethod]
        public JObject? ExpressListContracts(JArray @params)
        {
            var json = new JArray();
            foreach (var (hash, manifest) in ListContracts())
            {
                var jsonContract = new JObject();
                jsonContract["hash"] = hash.ToString();
                jsonContract["manifest"] = manifest.ToJson();
                json.Add(jsonContract);
            }
            return json;
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
            var index = start;
            while (true)
            {
                var hash = NativeContract.Ledger.GetBlockHash(snapshot, index)
                    ?? throw new Exception($"GetBlockHash for {index} returned null");
                var block = NativeContract.Ledger.GetTrimmedBlock(snapshot, hash)
                    ?? throw new Exception($"GetTrimmedBlock for {index} returned null");

                Debug.Assert(block.Index == index);

                if (index == 0 || block.Hashes.Length > 0)
                {
                    populatedBlocks.Add(index);
                }

                if (index == 0 || populatedBlocks.Count >= count) break;
                index--;
            }

            var response = new JObject();
            response["cacheId"] = cacheId;
            response["blocks"] = populatedBlocks;
            return response;
        }

        // ExpressGetNep17Contracts has been renamed ExpressGetTokenContracts,
        // but we keep the old method around for compat purposes
        [RpcMethod]
        public JObject ExpressGetNep17Contracts(JArray _) => ExpressListTokenContracts(_);

        [RpcMethod]
        public JObject ExpressListTokenContracts(JArray _)
        {
            var jsonContracts = new JArray();
            using var snapshot = neoSystem.GetSnapshot();
            foreach (var contract in snapshot.EnumerateTokenContracts(neoSystem.Settings))
            {
                var jsonContract = new JObject();
                jsonContracts.Add(contract.ToJson());
            }
            return jsonContracts;
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
        public JObject? ExpressCreateCheckpoint(JArray @params)
        {
            string filename = @params[0].AsString();

            if (chain.ConsensusNodes.Count > 1)
            {
                throw new Exception("Checkpoint create is only supported on single node express instances");
            }

            throw new NotImplementedException();
            // if (storageProvider is RocksDbStorageProvider rocksDbStorageProvider)
            // {
            //     rocksDbStorageProvider.CreateCheckpoint(filename, neoSystem.Settings, nodeAccountAddress);
            //     return filename;
            // }

            throw new Exception("Checkpoint create is only supported for RocksDb storage implementation");
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
            var tx = NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, neoSystem.Settings);
            return tx == null ? null : Convert.ToBase64String(tx.ToArray());
        }

        const int MAX_NOTIFICATIONS = 100;
        const string TRANSFER = "Transfer";

        [RpcMethod]
        public JObject ExpressEnumNotifications(JArray @params)
        {
            var contracts = ((JArray)@params[0]).Select(j => UInt160.Parse(j.AsString())).ToHashSet();
            var events = ((JArray)@params[1]).Select(j => j.AsString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int skip = @params.Count >= 3 ? (int)@params[2].AsNumber() : 0;
            int take = @params.Count >= 4 ? (int)@params[3].AsNumber() : MAX_NOTIFICATIONS;
            if (take > MAX_NOTIFICATIONS) take = MAX_NOTIFICATIONS;

            var notifications = GetNotifications(
                    SeekDirection.Backward,
                    contracts.Count > 0 ? contracts : null,
                    events.Count > 0 ? events : null)
                .Skip(skip);

            var count = 0;
            var jsonNotifications = new JArray();
            var truncated = false;
            foreach (var (blockIndex, _, notification) in notifications)
            {
                if (count++ > take)
                {
                    truncated = true;
                    break;
                }

                var jNotification = new JObject
                {
                    ["block-index"] = blockIndex,
                    ["script-hash"] = notification.ScriptHash.ToString(),
                    ["event-name"] = notification.EventName,
                    ["inventory-type"] = (byte)notification.InventoryType,
                    ["inventory-hash"] = notification.InventoryHash.ToString(),
                    ["state"] = Neo.VM.Helper.ToJson(notification.State),
                };
                jsonNotifications.Add(jNotification);
            }

            return new JObject
            {
                ["truncated"] = truncated,
                ["notifications"] = jsonNotifications,
            };
        }

        // Neo-express uses a custom implementation of GetApplicationLog due to
        // https://github.com/neo-project/neo-modules/issues/614
        [RpcMethod]
        public JObject GetApplicationLog(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            return GetAppLog(hash) ?? throw new Neo.Plugins.RpcException(-100, "Unknown transaction");
        }

        // Neo-Express uses a custom implementation of TokenTracker RPC methods. Originally, this was
        // because of https://github.com/neo-project/neo-modules/issues/614, but the custom implementation
        // has been maintained after TokenTracker was introduced (TokenTracker does not have the same
        // issue #614 that the older RpcNep17Tracker had) for compatibility w/ 3.0.x versions of neo-express.
        // Additionally, this implementation allows neo-express to introduce new RPC methods that depend
        // on notification processing (such as GetNep11Balances and GetNep11Transfers) on existing
        // neo-express blockchain instances.

        [RpcMethod]
        public JObject GetNep17Balances(JArray @params)
        {
            var scriptHash = AsScriptHash(@params[0]);

            using var snapshot = neoSystem.GetSnapshot();

            // collect the non-zero balances of all the deployed Nep17 contracts for the specified account
            var addressBalances = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == TokenStandard.Nep17)
                .Select(c => (
                    scriptHash: c.scriptHash,
                    balance: snapshot.GetNep17Balance(c.scriptHash, scriptHash, neoSystem.Settings)))
                .Where(t => !t.balance.IsZero)
                .ToList();

            // collect the last block index a transfer occurred for all account balances
            var updateIndexes = new Dictionary<UInt160, uint>();
            if (addressBalances.Count > 0)
            {
                var notifications = GetNotifications(
                    SeekDirection.Backward,
                    addressBalances.Select(b => b.scriptHash).ToHashSet(),
                    TRANSFER);

                foreach (var (blockIndex, _, notification) in notifications)
                {
                    // iterate backwards thru the notifications looking for all the Transfer events from a contract
                    // in assets where a Transfer event hasn't already been recorded
                    if (!updateIndexes.ContainsKey(notification.ScriptHash))
                    {
                        var transfer = TransferNotificationRecord.Create(notification);
                        if (transfer is not null
                            && (transfer.From == scriptHash || transfer.To == scriptHash))
                        {
                            // if the specified account was the sender or receiver of the current transfer,
                            // record the update index. Stop the iteration if indexes for all the assets are 
                            // have been recorded
                            updateIndexes.Add(notification.ScriptHash, blockIndex);
                            if (updateIndexes.Count == addressBalances.Count) break;
                        }
                    }
                }
            }

            var balances = new JArray();
            for (int i = 0; i < addressBalances.Count; i++)
            {
                var (assetHash, balance) = addressBalances[i];
                var lastUpdatedBlock = updateIndexes.TryGetValue(assetHash, out var _index) ? _index : 0;

                balances.Add(new JObject
                {
                    ["assethash"] = assetHash.ToString(),
                    ["amount"] = balance.ToString(),
                    ["lastupdatedblock"] = lastUpdatedBlock,
                });
            }

            return new JObject
            {
                ["address"] = Neo.Wallets.Helper.ToAddress(scriptHash, chain.AddressVersion),
                ["balance"] = balances,
            };
        }

        [RpcMethod]
        public JObject GetNep17Transfers(JArray @params) => GetTransfers(@params, TokenStandard.Nep17);

        class TokenEqualityComparer : IEqualityComparer<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId)>
        {
            public static TokenEqualityComparer Instance = new();

            private TokenEqualityComparer() { }

            public bool Equals((UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) x, (UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) y)
                => x.scriptHash.Equals(y.scriptHash)
                    && x.tokenId.Span.SequenceEqual(y.tokenId.Span);

            public int GetHashCode((UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) obj)
            {
                HashCode code = new();
                code.Add(obj.scriptHash);
                for (int i = 0; i < obj.tokenId.Length; i++)
                {
                    code.Add(obj.tokenId.Span[i]);
                }
                return code.ToHashCode();
            }
        }


        [RpcMethod]
        public JObject GetNep11Balances(JArray @params)
        {
            var scriptHash = AsScriptHash(@params[0]);

            using var snapshot = neoSystem.GetSnapshot();

            List<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId, BigInteger balance)> tokens = new();
            foreach (var contract in NativeContract.ContractManagement.ListContracts(snapshot))
            {
                if (!contract.Manifest.SupportedStandards.Contains("NEP-11")) continue;
                var balanceOf = contract.Manifest.Abi.GetMethod("balanceOf", -1);
                if (balanceOf is null) continue;
                var divisible = balanceOf.Parameters.Length == 2;

                foreach (var tokenId in snapshot.GetNep11Tokens(contract.Hash, scriptHash, neoSystem.Settings))
                {
                    var balance = divisible
                        ? snapshot.GetDivisibleNep11Balance(contract.Hash, tokenId, scriptHash, neoSystem.Settings)
                        : snapshot.GetIndivisibleNep11Owner(contract.Hash, tokenId, neoSystem.Settings) == scriptHash
                            ? BigInteger.One
                            : BigInteger.Zero;

                    if (balance.IsZero) continue;

                    tokens.Add((contract.Hash, tokenId, balance));
                }
            }

            // collect the last block index a transfer occurred for all tokens
            var updateIndexes = new Dictionary<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId), uint>(TokenEqualityComparer.Instance);
            if (tokens.Count > 0)
            {
                var notifications = GetNotifications(
                    SeekDirection.Backward,
                    tokens.Select(b => b.scriptHash).ToHashSet(),
                    TRANSFER);

                foreach (var (blockIndex, _, notification) in notifications)
                {
                    var transfer = TransferNotificationRecord.Create(notification);
                    if (transfer is null) continue;
                    if (transfer.From != scriptHash && transfer.To != scriptHash) continue;
                    if (transfer.TokenId.Length == 0) continue;
                    var key = (notification.ScriptHash, transfer.TokenId);
                    if (updateIndexes.ContainsKey(key)) continue;
                    updateIndexes.Add(key, blockIndex);
                    if (updateIndexes.Count == tokens.Count) break;
                }
            }

            var balances = new JArray();

            foreach (var asset in tokens.GroupBy(t => t.scriptHash))
            {
                var jsonTokens = new JArray();
                foreach (var (_, tokenId, balance) in asset)
                {
                    if (balance.IsZero) continue;

                    var lastUpdatedBlock = updateIndexes.TryGetValue((asset.Key, tokenId), out var value)
                        ? value : 0;
                    jsonTokens.Add(new JObject
                    {
                        ["tokenid"] = tokenId.Span.ToHexString(),
                        ["amount"] = balance.ToString(),
                        ["lastupdatedblock"] = lastUpdatedBlock
                    });
                }

                balances.Add(new JObject
                {
                    ["assethash"] = asset.ToString(),
                    ["tokens"] = jsonTokens,
                });
            }

            return new JObject
            {
                ["address"] = Neo.Wallets.Helper.ToAddress(scriptHash, chain.AddressVersion),
                ["balance"] = balances,
            };
        }

        [RpcMethod]
        public JObject GetNep11Transfers(JArray @params) => GetTransfers(@params, TokenStandard.Nep11);

        [RpcMethod]
        public JObject GetNep11Properties(JArray @params)
        {
            // logic replicated from TokenTracker.GetNep11Properties. 
            var nep11Hash = AsScriptHash(@params[0]);
            var tokenId = @params[1].AsString().HexToBytes();

            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(nep11Hash, "properties", CallFlags.ReadOnly, tokenId);

            using var snapshot = neoSystem.GetSnapshot();
            using var engine = ApplicationEngine.Run(builder.ToArray(), snapshot, settings: neoSystem.Settings);

            JObject json = new();
            if (engine.State == VMState.HALT)
            {
                var map = engine.ResultStack.Pop<Neo.VM.Types.Map>();
                foreach (var keyValue in map)
                {
                    if (keyValue.Value is Neo.VM.Types.CompoundType) continue;
                    var key = keyValue.Key.GetString() ?? string.Empty;
                    if (nep11PropertyNames.Contains(key))
                    {
                        json[key] = keyValue.Value.GetString();
                    }
                    else
                    {
                        json[key] = keyValue.Value.IsNull ? null : Convert.ToBase64String(keyValue.Value.GetSpan());
                    }
                }
            }
            return json;
        }

        [RpcMethod]
        public JObject ExpressPersistContract(JObject @params)
        {
            var state = RpcClient.ContractStateFromJson(@params[0]["state"]);
            var storagePairs = ((JArray)@params[0]["storage"])
                .Select(s => (s["key"].AsString(), s["value"].AsString()))
                .ToArray();
            var force = Enum.Parse<ContractCommand.OverwriteForce>(@params[0]["force"].AsString());

            return NodeUtility.PersistContract(neoSystem, state, storagePairs, force);
        }

        static readonly IReadOnlySet<string> nep11PropertyNames = new HashSet<string>
        {
            "name",
            "description",
            "image",
            "tokenURI"
        };

        JObject GetTransfers(JArray @params, TokenStandard standard)
        {
            // parse parameters
            var address = AsScriptHash(@params[0]);
            ulong startTime = @params.Count > 1
                ? (ulong)@params[1].AsNumber()
                : (DateTime.UtcNow - TimeSpan.FromDays(7)).ToTimestampMS();
            ulong endTime = @params.Count > 2
                ? (ulong)@params[2].AsNumber()
                : DateTime.UtcNow.ToTimestampMS();

            if (endTime < startTime) throw new Neo.Plugins.RpcException(-32602, "Invalid params");

            // iterate over the notifications to populate the send and receive arrays
            var sent = new JArray();
            var received = new JArray();

            using var snapshot = neoSystem.GetSnapshot();

            var contracts = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == standard)
                .Select(c => c.scriptHash)
                .ToHashSet();
            var notifications = GetNotifications(SeekDirection.Forward, contracts, TRANSFER);

            foreach (var (blockIndex, txIndex, notification) in notifications)
            {
                var header = NativeContract.Ledger.GetHeader(snapshot, blockIndex);
                if (startTime > header.Timestamp || header.Timestamp > endTime) continue;

                var transfer = TransferNotificationRecord.Create(notification);
                if (transfer is null) continue;
                if (transfer.From != address && transfer.To != address) continue;

                // create a JSON object to represent the transfer
                var jsonTransfer = new JObject()
                {
                    ["timestamp"] = header.Timestamp,
                    ["assethash"] = transfer.Asset.ToString(),
                    ["amount"] = transfer.Amount.ToString(),
                    ["blockindex"] = header.Index,
                    ["transfernotifyindex"] = txIndex,
                    ["txhash"] = transfer.Notification.InventoryHash.ToString(),
                };

                // NEP-11 transfer records include an extra field for the token ID (if present)
                if (standard == TokenStandard.Nep11 && transfer.TokenId.Length > 0)
                {
                    jsonTransfer["tokenid"] = transfer.TokenId.Span.ToHexString();
                }

                // add the json transfer object to send and/or receive collections as appropriate
                if (address == transfer.From)
                {
                    jsonTransfer["transferaddress"] = transfer.To.ToString();
                    sent.Add(jsonTransfer);
                }
                if (address == transfer.To)
                {
                    jsonTransfer["transferaddress"] = transfer.From.ToString();
                    received.Add(jsonTransfer);
                }
            }

            return new JObject
            {
                ["address"] = Neo.Wallets.Helper.ToAddress(address, chain.AddressVersion),
                ["sent"] = sent,
                ["received"] = received,
            };
        }

        UInt160 AsScriptHash(JObject json)
        {
            var text = json.AsString();
            return text.Length < 40
               ? Neo.Wallets.Helper.ToScriptHash(text, chain.AddressVersion)
               : UInt160.Parse(text);
        }
    }
}