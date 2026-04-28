// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressRpcServerPlugin.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Plugins.RpcServer;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Commands;
using NeoExpress.Models;
using NeoExpress.Validators;
using System.Collections.Immutable;
using System.Numerics;
using RpcException = Neo.Network.RPC.RpcException;

namespace NeoExpress.Node
{
    class ExpressRpcServerPlugin : Plugin
    {
        NeoSystem? neoSystem;
        RpcServer? rpcServer;
        readonly IExpressStorage expressStorage;
        readonly RpcServersSettings settings;
        readonly UInt160 nodeAccountAddress;
        readonly Lazy<ExpressPersistencePlugin> persistencePlugin;
        readonly CancellationTokenSource cancellationToken = new();
        readonly string cacheId;

        public CancellationToken CancellationToken => cancellationToken.Token;

        public ExpressRpcServerPlugin(RpcServersSettings settings, IExpressStorage expressStorage, UInt160 nodeAccountAddress)
        {
            this.expressStorage = expressStorage;
            this.settings = settings;
            this.nodeAccountAddress = nodeAccountAddress;

            cacheId = DateTimeOffset.Now.Ticks.ToString();
            persistencePlugin = new Lazy<ExpressPersistencePlugin>(() => (ExpressPersistencePlugin)Plugins.Single(p => p is ExpressPersistencePlugin));
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (this.neoSystem is not null)
                throw new Exception($"{nameof(OnSystemLoaded)} already called");
            neoSystem = system;
            rpcServer = new RpcServer(system, settings);
            rpcServer.RegisterMethods(this);
            rpcServer.StartRpcServer();

            base.OnSystemLoaded(system);
        }

        public override void Dispose()
        {
            rpcServer?.Dispose();
            base.Dispose();
        }

        [RpcMethod]
        public JObject ExpressShutdown(JArray @params)
        {
            const int SHUTDOWN_TIME = 2;

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var response = new JObject();
            response["process-id"] = proc.Id;

            Log($"ExpressShutdown requested. Shutting down in {SHUTDOWN_TIME} seconds", LogLevel.Info);
            cancellationToken.CancelAfter(TimeSpan.FromSeconds(SHUTDOWN_TIME));
            return response;
        }

        [RpcMethod]
        public JObject ExpressGetPopulatedBlocks(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            using var snapshot = neoSystem.GetSnapshotCache();
            var height = NativeContract.Ledger.CurrentIndex(snapshot);

            var count = OptionalUInt32Param(@params, 0, 20);
            count = count > 100 ? 100 : count;

            var start = OptionalUInt32Param(@params, 1, height);
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

                if (index == 0 || populatedBlocks.Count >= count)
                    break;
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
        public JArray ExpressGetNep17Contracts(JArray _) => ExpressListTokenContracts(_);

        [RpcMethod]
        public JArray ExpressListTokenContracts(JArray _)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));

            var jsonContracts = new JArray();
            using var snapshot = neoSystem.GetSnapshotCache();
            foreach (var contract in snapshot.EnumerateTokenContracts(neoSystem.Settings))
            {
                var jsonContract = new JObject();
                jsonContracts.Add(contract.ToJson());
            }
            return jsonContracts;
        }

        [RpcMethod]
        public JToken ExpressGetContractState(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            using var snapshot = neoSystem.GetSnapshotCache();

            var contractParam = RequiredParam(@params, 0);

            if (contractParam is JNumber number)
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

            var param = ParseParam(() => contractParam.AsString());

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
        public JArray? ExpressGetContractStorage(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var scriptHash = ParseUInt160Param(@params, 0);
            var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contract is null)
                return null;

            var storages = new JArray();
            byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
            using var snapshot = neoSystem.GetSnapshotCache();
            foreach (var (key, value) in snapshot.Find(prefix))
            {
                var storage = new JObject();
                storage["key"] = Convert.ToHexString(key.Key.Span);
                storage["value"] = Convert.ToHexString(value.Value.Span);
                storages.Add(storage);
            }
            return storages;
        }

        [RpcMethod]
        public JArray ExpressListContracts(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
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
        public JToken ExpressCreateCheckpoint(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            string filename = RequiredStringParam(@params, 0);

            if (neoSystem.Settings.ValidatorsCount > 1)
            {
                throw new NotSupportedException("Checkpoint create is only supported on single node express instances");
            }

            if (expressStorage is RocksDbExpressStorage rocksDbExpressStorage)
            {
                try
                {
                    rocksDbExpressStorage.CreateCheckpoint(filename, neoSystem.Settings.Network, neoSystem.Settings.AddressVersion, nodeAccountAddress);
                }
                catch (Exception ex) when (ex is ArgumentException
                    or System.IO.IOException
                    or NotSupportedException
                    or UnauthorizedAccessException)
                {
                    throw InvalidParams(ex);
                }
                return new JString(filename);
            }

            throw new NotSupportedException($"Checkpoint create is only supported for {nameof(RocksDbExpressStorage)}");
        }

        [RpcMethod]
        public JArray ExpressListOracleRequests(JArray _)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
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
        public JToken? ExpressCreateOracleResponseTx(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var jsonResponse = RequiredObjectParam(@params, 0);
            var response = ParseParam(() => new OracleResponse
            {
                Id = (ulong)RequiredProperty(jsonResponse, "id").AsNumber(),
                Code = (OracleResponseCode)RequiredProperty(jsonResponse, "code").AsNumber(),
                Result = Convert.FromBase64String(RequiredStringProperty(jsonResponse, "result"))
            });

            using var snapshot = neoSystem.GetSnapshotCache();
            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var oracleNodes = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Oracle, height);
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, neoSystem.Settings);
            return tx is null ? null : Convert.ToBase64String(tx.ToArray());
        }

        const int MAX_NOTIFICATIONS = 100;
        const string TRANSFER = "Transfer";

        [RpcMethod]
        public JObject ExpressEnumNotifications(JArray @params)
        {
            var contracts = RequiredArrayParam(@params, 0).Select(ParseUInt160Token).ToHashSet();
            var events = RequiredArrayParam(@params, 1).Select(j => RequiredString(j, "event filter")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var (skip, take) = GetNotificationPaging(@params);

            var notifications = persistencePlugin.Value
                .GetNotifications(
                    SeekDirection.Backward,
                    contracts.Count > 0 ? contracts : null,
                    events.Count > 0 ? events : null)
                .Skip(skip);

            return CreateNotificationsResponse(
                notifications.Select(n => (n.blockIndex, n.notification)),
                take);
        }

        internal static JObject CreateNotificationsResponse(
            IEnumerable<(uint blockIndex, NotificationRecord notification)> notifications,
            int take)
        {
            var count = 0;
            var jsonNotifications = new JArray();
            var truncated = false;
            foreach (var (blockIndex, notification) in notifications)
            {
                if (count++ >= take)
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
                    ["state"] = notification.State.ToJson(),
                };
                jsonNotifications.Add(jNotification);
            }

            return new JObject
            {
                ["truncated"] = truncated,
                ["notifications"] = jsonNotifications,
            };
        }

        internal static (int skip, int take) GetNotificationPaging(JArray @params)
        {
            int skip = OptionalInt32NumberParam(@params, 2, 0);
            int take = OptionalInt32NumberParam(@params, 3, MAX_NOTIFICATIONS);
            if (skip < 0 || take < 0)
                throw new RpcException(-32602, "Invalid params");
            if (take > MAX_NOTIFICATIONS)
                take = MAX_NOTIFICATIONS;

            return (skip, take);
        }

        // Neo-express uses a custom implementation of GetApplicationLog due to
        // https://github.com/neo-project/neo-modules/issues/614
        [RpcMethod]
        public JObject GetApplicationLog(JArray _params)
        {
            UInt256 hash = ParseUInt256Param(_params, 0);
            return persistencePlugin.Value.GetAppLog(hash) ?? throw new RpcException(-100, "Unknown transaction/blockhash");
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
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var address = AsScriptHash(RequiredParam(@params, 0));

            using var snapshot = neoSystem.GetSnapshotCache();

            // collect the non-zero balances of all the deployed Nep17 contracts for the specified account
            var addressBalances = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == TokenStandard.Nep17)
                .Select(c => (
                    scriptHash: c.scriptHash,
                    balance: snapshot.GetNep17Balance(c.scriptHash, address, neoSystem.Settings)))
                .Where(t => !t.balance.IsZero)
                .ToList();

            // collect the last block index a transfer occurred for all account balances
            var updateIndexes = new Dictionary<UInt160, uint>();
            if (addressBalances.Count > 0)
            {
                var notifications = persistencePlugin.Value.GetNotifications(
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
                            && (transfer.From == address || transfer.To == address))
                        {
                            // if the specified account was the sender or receiver of the current transfer,
                            // record the update index. Stop the iteration if indexes for all the assets are
                            // have been recorded
                            updateIndexes.Add(notification.ScriptHash, blockIndex);
                            if (updateIndexes.Count == addressBalances.Count)
                                break;
                        }
                    }
                }
            }

            var balances = new JArray();
            for (int i = 0; i < addressBalances.Count; i++)
            {
                var (scriptHash, balance) = addressBalances[i];
                var lastUpdatedBlock = updateIndexes.TryGetValue(scriptHash, out var _index) ? _index : 0;
                var details = GetTokenDetails(snapshot, scriptHash);

                balances.Add(new JObject
                {
                    ["assethash"] = scriptHash.ToString(),
                    ["name"] = details.name,
                    ["symbol"] = details.symbol,
                    ["decimals"] = details.decimals.ToString(),
                    ["amount"] = balance.ToString(),
                    ["lastupdatedblock"] = lastUpdatedBlock,
                });
            }

            return new JObject
            {
                ["address"] = address.ToAddress(neoSystem.Settings.AddressVersion),
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
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var address = AsScriptHash(RequiredParam(@params, 0));

            using var snapshot = neoSystem.GetSnapshotCache();

            List<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId, BigInteger balance)> tokens = new();
            foreach (var contract in NativeContract.ContractManagement.ListContracts(snapshot))
            {
                if (!contract.Manifest.SupportedStandards.Contains("NEP-11"))
                    continue;
                var balanceOf = contract.Manifest.Abi.GetMethod("balanceOf", -1);
                if (balanceOf is null)
                    continue;
                var divisible = balanceOf.Parameters.Length == 2;

                foreach (var tokenId in snapshot.GetNep11Tokens(contract.Hash, address, neoSystem.Settings))
                {
                    var balance = divisible
                        ? snapshot.GetDivisibleNep11Balance(contract.Hash, tokenId, address, neoSystem.Settings)
                        : snapshot.GetIndivisibleNep11Owner(contract.Hash, tokenId, neoSystem.Settings) == address
                            ? BigInteger.One
                            : BigInteger.Zero;

                    if (balance.IsZero)
                        continue;

                    tokens.Add((contract.Hash, tokenId, balance));
                }
            }

            // collect the last block index a transfer occurred for all tokens
            var updateIndexes = new Dictionary<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId), uint>(TokenEqualityComparer.Instance);
            if (tokens.Count > 0)
            {
                var notifications = persistencePlugin.Value.GetNotifications(
                    SeekDirection.Backward,
                    tokens.Select(b => b.scriptHash).ToHashSet(),
                    TRANSFER);

                foreach (var (blockIndex, _, notification) in notifications)
                {
                    var transfer = TransferNotificationRecord.Create(notification);
                    if (transfer is null)
                        continue;
                    if (transfer.From != address && transfer.To != address)
                        continue;
                    if (transfer.TokenId.Length == 0)
                        continue;
                    var key = (notification.ScriptHash, transfer.TokenId);
                    if (updateIndexes.ContainsKey(key))
                        continue;
                    updateIndexes.Add(key, blockIndex);
                    if (updateIndexes.Count == tokens.Count)
                        break;
                }
            }

            var balances = new JArray();

            foreach (var asset in tokens.GroupBy(t => t.scriptHash))
            {
                var details = GetTokenDetails(snapshot, asset.Key);

                var jsonTokens = new JArray();
                foreach (var (_, tokenId, balance) in asset)
                {
                    if (balance.IsZero)
                        continue;

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
                    ["assethash"] = asset.Key.ToString(),
                    ["name"] = details.name,
                    ["symbol"] = details.symbol,
                    ["decimals"] = details.decimals.ToString(),
                    ["tokens"] = jsonTokens,
                });
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
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            // logic replicated from TokenTracker.GetNep11Properties.
            var nep11Hash = AsScriptHash(RequiredParam(@params, 0));
            var tokenId = ParseParam(() => RequiredParam(@params, 1).AsString().HexToBytes());

            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(nep11Hash, "properties", CallFlags.ReadOnly, tokenId);

            using var snapshot = neoSystem.GetSnapshotCache();
            using var engine = ApplicationEngine.Run(builder.ToArray(), snapshot, settings: neoSystem.Settings);

            JObject json = new();
            if (engine.State == VMState.HALT)
            {
                var map = engine.ResultStack.Pop<Neo.VM.Types.Map>();
                foreach (var keyValue in map)
                {
                    if (keyValue.Value is Neo.VM.Types.CompoundType)
                        continue;
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
        public JToken ExpressPersistContract(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var payload = RequiredObjectParam(@params, 0);
            var state = ParseParam(() => RpcClient.ContractStateFromJson(RequiredObjectProperty(payload, "state")));
            var storagePairs = RequiredArrayProperty(payload, "storage")
                .Select(s =>
                {
                    var entry = RequiredObject(s, "storage entry");
                    return (
                        RequiredStringProperty(entry, "key"),
                        RequiredStringProperty(entry, "value"));
                }).ToArray();

            var force = ParseParam(() => Enum.Parse<ContractCommand.OverwriteForce>(RequiredStringProperty(payload, "force")));

            return NodeUtility.PersistContract(neoSystem, state, storagePairs, force);
        }

        [RpcMethod]
        public JToken ExpressPersistStorage(JArray @params)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var payload = RequiredObjectParam(@params, 0);
            var state = ParseParam(() => RpcClient.ContractStateFromJson(RequiredObjectProperty(payload, "state")));
            var storagePairs = RequiredArrayProperty(payload, "storage")
                .Select(s =>
                {
                    var entry = RequiredObject(s, "storage entry");
                    return (
                        RequiredStringProperty(entry, "key"),
                        RequiredStringProperty(entry, "value"));
                }).ToArray();

            var force = ParseParam(() => Enum.Parse<ContractCommand.OverwriteForce>(RequiredStringProperty(payload, "force")));

            JToken result = 0;
            foreach (var pair in storagePairs)
            {
                result = NodeUtility.PersistStorageKeyValuePair(neoSystem, state, pair, force);
            }
            return result;
        }

        [RpcMethod]
        public JToken ExpressIsNep11Compliant(JToken @param)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var nep11Hash = AsScriptHash(@param);

            var nep11 = new Nep11Token(neoSystem.Settings, neoSystem.GetSnapshotCache(), nep11Hash);

            return nep11.HasValidMethods() &&
                nep11.IsSymbolValid() &&
                nep11.IsDecimalsValid() &&
                nep11.IsBalanceOfValid();
        }

        [RpcMethod]
        public JToken ExpressIsNep17Compliant(JToken @param)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            var nep17Hash = AsScriptHash(@param);

            var nep17 = new Nep17Token(neoSystem.Settings, neoSystem.GetSnapshotCache(), nep17Hash);

            return nep17.HasValidMethods() &&
                nep17.IsSymbolValid() &&
                nep17.IsDecimalsValid() &&
                nep17.IsBalanceOfValid();
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
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            // parse parameters
            var address = AsScriptHash(RequiredParam(@params, 0));
            ulong startTime = OptionalUInt64NumberParam(@params, 1, (DateTime.UtcNow - TimeSpan.FromDays(7)).ToTimestampMS());
            ulong endTime = OptionalUInt64NumberParam(@params, 2, DateTime.UtcNow.ToTimestampMS());

            if (endTime < startTime)
                throw new RpcException(-32602, "Invalid params");

            // iterate over the notifications to populate the send and receive arrays
            var sent = new JArray();
            var received = new JArray();

            using var snapshot = neoSystem.GetSnapshotCache();

            var contracts = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == standard)
                .Select(c => c.scriptHash)
                .ToHashSet();
            var notifications = persistencePlugin.Value.GetNotifications(SeekDirection.Forward, contracts, TRANSFER);

            foreach (var (blockIndex, txIndex, notification) in notifications)
            {
                var header = NativeContract.Ledger.GetHeader(snapshot, blockIndex);
                if (startTime > header.Timestamp || header.Timestamp > endTime)
                    continue;

                var transfer = TransferNotificationRecord.Create(notification);
                if (transfer is null)
                    continue;
                if (transfer.From != address && transfer.To != address)
                    continue;

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
                ["address"] = address.ToAddress(neoSystem.Settings.AddressVersion),
                ["sent"] = sent,
                ["received"] = received,
            };
        }

        const int INVALID_PARAMS = -32602;

        static RpcException InvalidParams(Exception ex) => InvalidParams(ex.Message);

        static RpcException InvalidParams(string message)
            => new(INVALID_PARAMS, $"Invalid params: {message}");

        static T ParseParam<T>(Func<T> parse)
        {
            try
            {
                return parse();
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException
                or FormatException
                or IndexOutOfRangeException
                or InvalidCastException
                or NullReferenceException
                or OverflowException)
            {
                throw InvalidParams(ex);
            }
        }

        static JToken RequiredParam(JArray @params, int index)
        {
            if (index < 0 || index >= @params.Count || @params[index] is null)
                throw InvalidParams($"missing parameter {index}");
            return @params[index]!;
        }

        static JArray RequiredArrayParam(JArray @params, int index)
            => RequiredArray(RequiredParam(@params, index), $"parameter {index}");

        static JObject RequiredObjectParam(JArray @params, int index)
            => RequiredObject(RequiredParam(@params, index), $"parameter {index}");

        static string RequiredStringParam(JArray @params, int index)
            => RequiredString(RequiredParam(@params, index), $"parameter {index}");

        static JToken RequiredProperty(JObject json, string name)
        {
            var value = json[name];
            if (value is null)
                throw InvalidParams($"missing '{name}'");
            return value;
        }

        static JArray RequiredArrayProperty(JObject json, string name)
            => RequiredArray(RequiredProperty(json, name), $"'{name}'");

        static JObject RequiredObjectProperty(JObject json, string name)
            => RequiredObject(RequiredProperty(json, name), $"'{name}'");

        static string RequiredStringProperty(JObject json, string name)
            => RequiredString(RequiredProperty(json, name), $"'{name}'");

        static JArray RequiredArray(JToken? json, string name)
            => json is JArray array ? array : throw InvalidParams($"{name} must be an array");

        static JObject RequiredObject(JToken? json, string name)
            => json is JObject obj ? obj : throw InvalidParams($"{name} must be an object");

        static string RequiredString(JToken? json, string name)
        {
            if (json is null)
                throw InvalidParams($"missing {name}");
            return ParseParam(json.AsString);
        }

        static uint OptionalUInt32Param(JArray @params, int index, uint defaultValue)
            => @params.Count > index ? ParseParam(() => uint.Parse(RequiredStringParam(@params, index))) : defaultValue;

        static int OptionalInt32NumberParam(JArray @params, int index, int defaultValue)
            => @params.Count > index ? ParseParam(() => (int)RequiredParam(@params, index).AsNumber()) : defaultValue;

        static ulong OptionalUInt64NumberParam(JArray @params, int index, ulong defaultValue)
            => @params.Count > index ? ParseParam(() => (ulong)RequiredParam(@params, index).AsNumber()) : defaultValue;

        static UInt160 ParseUInt160Param(JArray @params, int index)
            => ParseUInt160Token(RequiredParam(@params, index));

        static UInt160 ParseUInt160Token(JToken? json)
        {
            if (json is null)
                throw InvalidParams("missing value");
            return ParseParam(() => UInt160.Parse(json.AsString()));
        }

        static UInt256 ParseUInt256Param(JArray @params, int index)
            => ParseParam(() => UInt256.Parse(RequiredParam(@params, index).AsString()));

        UInt160 AsScriptHash(JToken json)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            return ParseParam(() =>
            {
                var text = json.AsString();
                return text.Length < 40
                   ? text.ToScriptHash(neoSystem.Settings.AddressVersion)
                   : UInt160.Parse(text);
            });
        }

        (string name, string symbol, byte decimals) GetTokenDetails(DataCache snapshot, UInt160 tokenHash)
        {
            if (neoSystem is null)
                throw new NullReferenceException(nameof(neoSystem));
            return snapshot.TryGetTokenDetails(tokenHash, neoSystem.Settings, out var details)
                ? details
                : ("<Unknown>", "<UNK>", (byte)0);
        }
    }
}
