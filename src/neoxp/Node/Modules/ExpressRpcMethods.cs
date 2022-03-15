using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using ByteString = Neo.VM.Types.ByteString;

namespace NeoExpress.Node
{
    class ExpressRpcMethods
    {
        readonly NeoSystem neoSystem;
        readonly IStorageProvider storageProvider;
        readonly UInt160 nodeAccountAddress;
        readonly CancellationTokenSource cancellationToken;
        readonly string cacheId;

        public ExpressRpcMethods(NeoSystem neoSystem, IStorageProvider storageProvider, UInt160 nodeAccountAddress, CancellationTokenSource cancellationToken)
        {
            this.neoSystem = neoSystem;
            this.storageProvider = storageProvider;
            this.nodeAccountAddress = nodeAccountAddress;
            this.cancellationToken = cancellationToken;
            cacheId = DateTimeOffset.Now.Ticks.ToString();
        }

        [RpcMethod]
        public JObject ExpressShutdown(JArray @params)
        {
            const int SHUTDOWN_TIME = 2;

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var response = new JObject();
            response["process-id"] = proc.Id;

            Utility.Log(nameof(ExpressRpcMethods), LogLevel.Info, $"ExpressShutdown requested. Shutting down in {SHUTDOWN_TIME} seconds");
            cancellationToken.CancelAfter(TimeSpan.FromSeconds(SHUTDOWN_TIME));
            return response;
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

                System.Diagnostics.Debug.Assert(block.Index == index);

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

            if (storageProvider is RocksDbStorageProvider rocksDbStorageProvider)
            {
                rocksDbStorageProvider.CreateCheckpoint(filename, neoSystem.Settings, nodeAccountAddress);
                return filename;
            }

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
            var tx = ExpressOracle.CreateResponseTx(snapshot, request, response, oracleNodes, neoSystem.Settings);
            return tx == null ? null : Convert.ToBase64String(tx.ToArray());
        }

        // Neo-express uses a custom implementation of GetApplicationLog due to
        // https://github.com/neo-project/neo-modules/issues/614
        [RpcMethod]
        public JObject GetApplicationLog(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            return PersistencePlugin.GetAppLog(storageProvider, hash) ?? throw new RpcException(-100, "Unknown transaction");
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
            var address = AsScriptHash(@params[0]);

            // collect a list of assets the address has either sent or received
            // and the last block index a transfer occurred
            var assets = new Dictionary<UInt160, uint>();
            using var snapshot = neoSystem.GetSnapshot();
            foreach (var (blockIndex, _, transfer) in PersistencePlugin.GetTransferNotifications(snapshot, storageProvider, TokenStandard.Nep17, address))
            {
                assets[transfer.Asset] = blockIndex;
            }

            // get current balance for each asset with at least one transfer record
            var balances = new JArray();
            foreach (var (asset, lastUpdatedBlock) in assets)
            {
                if (snapshot.TryGetNep17Balance(asset, address, neoSystem.Settings, out var balance))
                {
                    balances.Add(new JObject
                    {
                        ["assethash"] = asset.ToString(),
                        ["amount"] = balance.ToString(),
                        ["lastupdatedblock"] = lastUpdatedBlock,
                    });
                }
            }

            return new JObject
            {
                ["address"] = address.ToAddress(neoSystem.Settings.AddressVersion),
                ["balance"] = balances,
            };
        }

        [RpcMethod]
        public JObject GetNep17Transfers(JArray @params) => GetTransfers(@params, TokenStandard.Nep17);

        [RpcMethod]
        public JObject GetNep11Balances(JArray @params)
        {
            var address = AsScriptHash(@params[0]);

            // collect a list of assets + tokens that address has sent or received
            // and the last block index a transfer occurred

            var assets = new Dictionary<UInt160, Dictionary<ByteString, uint>>();
            using var snapshot = neoSystem.GetSnapshot();
            foreach (var (blockIndex, _, transfer) in PersistencePlugin.GetTransferNotifications(snapshot, storageProvider, TokenStandard.Nep11, address))
            {
                if (transfer.TokenId == null) continue;
                var tokens = assets.GetOrAdd(transfer.Asset, _ => new Dictionary<ByteString, uint>());
                tokens[transfer.TokenId] = blockIndex;
            }

            JArray balances = new();
            foreach (var (asset, tokens) in assets)
            {
                // Balance logic is different for divisible and non-divisible NFTs
                if (snapshot.TryGetDecimals(asset, neoSystem.Settings, out var decimals))
                {
                    JArray jsonTokens = new();
                    foreach (var (token, lastUpdatedBlock) in tokens)
                    {
                        if (decimals == 0)
                        {
                            // for non divisible tokens, check to see if the token owner 
                            // matches the current address
                            if (snapshot.TryGetIndivisibleNep11Owner(asset, token, neoSystem.Settings, out var owner)
                                && owner == address)
                            {
                                jsonTokens.Add(new JObject
                                {
                                    ["tokenid"] = token.GetSpan().ToHexString(),
                                    ["amount"] = BigInteger.One.ToString(),
                                    ["lastupdatedblock"] = lastUpdatedBlock
                                });
                            }
                        }
                        else
                        {
                            // for divisible NFTs, get the asset/token balance for this address 
                            if (snapshot.TryGetDivisibleNep11Balance(asset, token, address, neoSystem.Settings, out var balance)
                                && balance > BigInteger.Zero)
                            {
                                jsonTokens.Add(new JObject
                                {
                                    ["tokenid"] = token.GetSpan().ToHexString(),
                                    ["amount"] = balance.ToString(),
                                    ["lastupdatedblock"] = lastUpdatedBlock
                                });
                            }
                        }
                    }

                    if (jsonTokens.Count > 0)
                    {
                        balances.Add(new JObject
                        {
                            ["assethash"] = asset.ToString(),
                            ["tokens"] = jsonTokens,
                        });
                    }
                }
            }

            return new JObject
            {
                ["address"] = address.ToAddress(neoSystem.Settings.AddressVersion),
                ["balance"] = balances,
            };
        }

        [RpcMethod]
        public JObject GetNep11Transfers(JArray @params) => GetTransfers(@params, TokenStandard.Nep11);

        [RpcMethod]
        public JObject GetNep11Properties(JArray @params)
        {
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
        
        private const byte Prefix_Contract = 8;
        private const byte Prefix_NextAvailableId = 15;

        private int LastUsedContractId(DataCache snapshot)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
            StorageItem item = snapshot.TryGet(key);
            return (int)(BigInteger)item;
        }

        private void SetLastUsedContractId(DataCache snapshot, int newId)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
            StorageItem item = snapshot.GetAndChange(key);
            item.Set(newId);
        }
        
        private int GetNextAvailableId(DataCache snapshot)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
            StorageItem item = snapshot.GetAndChange(key);
            int value = (int)(BigInteger)item;
            item.Add(1);
            return value;
        }
        
        [RpcMethod]
        public JObject? ExpressPersistContract(JObject @params)
        {
            var state = @params[0]["state"];
            var storagePairs = (JArray) @params[0]["storage"];
            var snapshot = neoSystem.GetSnapshot();

            var nef = new NefFile
            {
                CheckSum = (uint)state["nef"]["checksum"].AsNumber(),
                Compiler = state["nef"]["compiler"].AsString(),
                Source = state["nef"]["source"].AsString(),
                Tokens = new MethodToken[0], // TODO: support parsing MethodTokens or use fix of https://github.com/neo-project/neo/issues/2674
                Script = Convert.FromBase64String(state["nef"]["script"].AsString())
            };
            
            ContractManifest m = ContractManifest.FromJson(state["manifest"]);
            
            // Our local chain might already be using the contract id of the pulled contract, we need to check for this
            // to avoid having contracts with duplicate id's. This is important because the contract id is part of the
            // StorageContext used with Storage syscalls and else we'll potentially override storage keys or iterate
            // over keys that shouldn't exist for one of the contracts.
            int contractId = (int)state["id"].AsNumber(); 
            if (contractId <= LastUsedContractId(snapshot))
            {
                contractId = GetNextAvailableId(snapshot);
            }
            else
            {
                // Update available id such that a regular contract deploy will use the right next id;
                SetLastUsedContractId(snapshot, contractId);
            }
            
            var c = new ContractState
            {
                Id = contractId,
                Hash = UInt160.Parse(state["hash"].AsString()),
                UpdateCounter = (ushort)state["updatecounter"].AsNumber(),
                Nef = nef,
                Manifest = m
            };
            
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(c.Hash);
            snapshot.Add(key, new StorageItem(c));
            
            foreach (var pair in storagePairs)
            {
                snapshot.Add(
                    new StorageKey { Id = c.Id, Key = Convert.FromBase64String(pair["k"].AsString())}, 
                    new StorageItem(Convert.FromBase64String(pair["v"].AsString()))
                );
            }

            snapshot.Commit();

            return true;
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

            if (endTime < startTime) throw new RpcException(-32602, "Invalid params");

            // iterate over the notifications to populate the send and receive arrays
            var sent = new JArray();
            var received = new JArray();

            using var snapshot = neoSystem.GetSnapshot();
            foreach (var (blockIndex, txIndex, transfer) in PersistencePlugin.GetTransferNotifications(snapshot, storageProvider, standard, address))
            {
                var header = NativeContract.Ledger.GetHeader(snapshot, blockIndex);
                if (startTime <= header.Timestamp || header.Timestamp <= endTime)
                {
                    // create a JSON object to represent the transfer
                    var json = new JObject()
                    {
                        ["timestamp"] = header.Timestamp,
                        ["assethash"] = transfer.Asset.ToString(),
                        ["amount"] = transfer.Amount.ToString(),
                        ["blockindex"] = header.Index,
                        ["transfernotifyindex"] = txIndex,
                        ["txhash"] = transfer.Notification.InventoryHash.ToString(),
                    };

                    // NEP-11 transfer records include an extra field for the token ID (if present)
                    if (standard == TokenStandard.Nep11
                        && transfer.TokenId != null)
                    {
                        json["tokenid"] = transfer.TokenId.GetSpan().ToHexString();
                    }

                    // add the json transfer object to send and/or receive collections as appropriate
                    if (address == transfer.From)
                    {
                        json["transferaddress"] = transfer.To.ToString();
                        sent.Add(json);
                    }
                    if (address == transfer.To)
                    {
                        json["transferaddress"] = transfer.From.ToString();
                        received.Add(json);
                    }
                }
            }

            return new JObject
            {
                ["address"] = address.ToAddress(neoSystem.Settings.AddressVersion),
                ["sent"] = sent,
                ["received"] = received,
            };
        }

        UInt160 AsScriptHash(JObject json)
        {
            var text = json.AsString();
            return text.Length < 40
               ? text.ToScriptHash(neoSystem.Settings.AddressVersion)
               : UInt160.Parse(text);
        }
    }
}
