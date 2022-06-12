using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;
using NeoExpress.Models;
using static Neo.SmartContract.Native.NativeContract;

namespace NeoExpress.Node
{

    // Breaking Changes:
    //  * ExpressCreateOracleResponseTx replaced with ExpressSubmitOracleResponseAsync

    partial class ExpressSystem
    {
        [RpcMethod]
        public JObject ExpressShutdown(JArray @params)
        {
            const int SHUTDOWN_TIME = 2;

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var response = new JObject();
            response["process-id"] = proc.Id;

            Utility.Log(nameof(ExpressSystem), LogLevel.Info, $"ExpressShutdown requested. Shutting down in {SHUTDOWN_TIME} seconds");
            shutdownTokenSource.CancelAfter(TimeSpan.FromSeconds(SHUTDOWN_TIME));
            return response;
        }

        [RpcMethod]
        public JObject ExpressGetPopulatedBlocks(JArray @params)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var height = Ledger.CurrentIndex(snapshot);

            var count = @params.Count >= 1 ? uint.Parse(@params[0].AsString()) : 20;
            count = count > 100 ? 100 : count;

            var start = @params.Count >= 2 ? uint.Parse(@params[1].AsString()) : height;
            start = start > height ? height : start;

            var populatedBlocks = new JArray();
            var index = start;
            while (true)
            {
                var hash = Ledger.GetBlockHash(snapshot, index)
                    ?? throw new Exception($"GetBlockHash for {index} returned null");
                var block = Ledger.GetTrimmedBlock(snapshot, hash)
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

        [RpcMethod]
        public JObject? ExpressCreateCheckpoint(JArray @params)
        {
            string path = @params[0].AsString();
            CreateCheckpoint(path);
            return path;
        }

        [RpcMethod]
        public JObject? ExpressListContracts(JArray @params)
        {
            var contracts = ListContracts();

            var json = new JArray();
            foreach (var contract in contracts)
            {
                json.Add(new JObject()
                {
                    ["hash"] = contract.hash.ToString(),
                    ["manifest"] = contract.manifest.ToJson()
                });
            }
            return json;
        }

        [RpcMethod]
        public JObject ExpressListTokenContracts(JArray _)
        {

            var json = new JArray();
            foreach (var contract in ListTokenContracts())
            {
                json.Add(contract.ToJson());
            }
            return json;
        }

        // ExpressGetNep17Contracts has been renamed ExpressGetTokenContracts,
        // but we keep the old method around for compat purposes
        [RpcMethod]
        public JObject ExpressGetNep17Contracts(JArray _) => ExpressListTokenContracts(_);

        [RpcMethod]
        public JObject? ExpressListOracleRequests(JArray _)
        {
            var json = new JArray();
            foreach (var (requestId, request) in ListOracleRequests())
            {
                json.Add(new JObject()
                {
                    ["requestid"] = requestId,
                    ["originaltxid"] = $"{request.OriginalTxid}",
                    ["gasforresponse"] = request.GasForResponse,
                    ["url"] = request.Url,
                    ["filter"] = request.Filter,
                    ["callbackcontract"] = $"{request.CallbackContract}",
                    ["callbackmethod"] = request.CallbackMethod,
                    ["userdata"] = Convert.ToBase64String(request.UserData),
                });
            }
            return json;
        }

        [RpcMethod]
        public JObject? ExpressGetContractStorage(JArray @params)
        {
            var scriptHash = UInt160.Parse(@params[0].AsString());
            var json = new JArray();
            foreach (var (key, value) in ListStorages(scriptHash))
            {
                json.Add(new JObject()
                {
                    ["key"] = Convert.ToBase64String(key.Span),
                    ["value"] = Convert.ToBase64String(value.Span),
                });
            }
            return json;
        }

        [RpcMethod]
        public async Task<JObject?> ExpressFastForwardAsync(JArray @params)
        {
            var blockCount = (uint)@params[0].AsNumber();
            var timestampDelta = TimeSpan.Parse(@params[1].AsString());
            await FastForwardAsync(blockCount, timestampDelta).ConfigureAwait(false);
            return true;
        }

        [RpcMethod]
        public async Task<JObject?> ExpressSubmitOracleResponseAsync(JArray @params)
        {
            var response = JsonToOracleResponse(@params[0]);
            var txHash = await SubmitOracleResponseAsync(response).ConfigureAwait(false);
            return $"{txHash}";

            static OracleResponse JsonToOracleResponse(JObject json)
            {
                var id = (ulong)json["id"].AsNumber();
                var code = (OracleResponseCode)json["code"].AsNumber();
                var result = Convert.FromBase64String(json["result"].AsString());
                return new OracleResponse()
                {
                    Id = id,
                    Code = code,
                    Result = result
                };
            }
        }

        [RpcMethod]
        public JObject ExpressPersistContract(JObject @params)
        {
            var state = Neo.Network.RPC.RpcClient.ContractStateFromJson(@params[0]["state"]);
            var storagePairs = ((JArray)@params[0]["storage"])
                .Select(s => (
                    s["key"].AsString(),
                    s["value"].AsString())
                ).ToArray();
            var force = Enum.Parse<Commands.ContractCommand.OverwriteForce>(@params[0]["force"].AsString());

            return PersistContract(state, storagePairs, force);
        }

        [RpcMethod]
        public JObject ExpressEnumNotifications(JArray @params)
        {
            const int MAX_NOTIFICATIONS = 100;

            var contracts = ((JArray)@params[0]).Select(j => UInt160.Parse(j.AsString())).ToHashSet();
            var events = ((JArray)@params[1]).Select(j => j.AsString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int skip = @params.Count >= 3 ? (int)@params[2].AsNumber() : 0;
            int take = @params.Count >= 4 ? (int)@params[3].AsNumber() : MAX_NOTIFICATIONS;
            take = Math.Min(take, MAX_NOTIFICATIONS);

            var notifications = GetNotifications(SeekDirection.Backward, contracts, events).Skip(skip);
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

                jsonNotifications.Add(new JObject
                {
                    ["block-index"] = blockIndex,
                    ["script-hash"] = notification.ScriptHash.ToString(),
                    ["event-name"] = notification.EventName,
                    ["inventory-type"] = (byte)notification.InventoryType,
                    ["inventory-hash"] = notification.InventoryHash.ToString(),
                    ["state"] = Neo.VM.Helper.ToJson(notification.State),
                });
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
            var log = GetApplicationLog(hash) ?? throw new RpcException(-100, "Unknown transaction");
            return log.ToJson();
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
            if (neoSystem is null) throw new NullReferenceException(nameof(neoSystem));
            var account = AsScriptHash(@params[0]);

            using var snapshot = neoSystem.GetSnapshot();

            // collect the non-zero balances of all the deployed Nep17 contracts for the specified account
            var accountBalances = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == TokenStandard.Nep17)
                .Select(c => (
                    scriptHash: c.scriptHash,
                    balance: snapshot.GetNep17Balance(c.scriptHash, account, neoSystem.Settings)))
                .Where(t => !t.balance.IsZero)
                .ToList();

            // collect the last block index a transfer occurred for all account balances
            var updateIndexes = new Dictionary<UInt160, uint>();
            if (accountBalances.Count > 0)
            {
                var notifications = GetNotifications(
                    SeekDirection.Backward,
                    accountBalances.Select(b => b.scriptHash).ToHashSet(),
                    TRANSFER);

                foreach (var (blockIndex, _, notification) in notifications)
                {
                    // iterate backwards thru the notifications looking for all the Transfer events from a contract
                    // in assets where a Transfer event hasn't already been recorded
                    if (!updateIndexes.ContainsKey(notification.ScriptHash))
                    {
                        var transfer = TransferNotificationRecord.Create(notification);
                        if (transfer is not null
                            && (transfer.From == account || transfer.To == account))
                        {
                            // if the specified account was the sender or receiver of the current transfer,
                            // record the update index. Stop the iteration if indexes for all the assets are 
                            // have been recorded
                            updateIndexes.Add(notification.ScriptHash, blockIndex);
                            if (updateIndexes.Count == accountBalances.Count) break;
                        }
                    }
                }
            }

            var jsonBalances = new JArray();
            for (int i = 0; i < accountBalances.Count; i++)
            {
                var (assetHash, balance) = accountBalances[i];
                var lastUpdatedBlock = updateIndexes.TryGetValue(assetHash, out var _index) ? _index : 0;

                jsonBalances.Add(new JObject
                {
                    ["assethash"] = assetHash.ToString(),
                    ["amount"] = balance.ToString(),
                    ["lastupdatedblock"] = lastUpdatedBlock,
                });
            }

            return new JObject
            {
                ["address"] = AsAddress(account),
                ["balance"] = jsonBalances,
            };
        }

        [RpcMethod]
        public JObject GetNep17Transfers(JArray @params) => GetTransfers(@params, TokenStandard.Nep17);

        [RpcMethod]
        public JObject GetNep11Balances(JArray @params)
        {
            if (neoSystem is null) throw new NullReferenceException(nameof(neoSystem));
            var account = AsScriptHash(@params[0]);

            using var snapshot = neoSystem.GetSnapshot();

            List<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId, BigInteger balance)> tokens = new();
            foreach (var contract in ContractManagement.ListContracts(snapshot))
            {
                if (!contract.Manifest.SupportedStandards.Contains("NEP-11")) continue;
                var balanceOf = contract.Manifest.Abi.GetMethod("balanceOf", -1);
                if (balanceOf is null) continue;
                var divisible = balanceOf.Parameters.Length == 2;

                foreach (var tokenId in snapshot.GetNep11Tokens(contract.Hash, account, neoSystem.Settings))
                {
                    var balance = divisible
                        ? snapshot.GetDivisibleNep11Balance(contract.Hash, tokenId, account, neoSystem.Settings)
                        : snapshot.GetIndivisibleNep11Owner(contract.Hash, tokenId, neoSystem.Settings) == account
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
                    if (transfer.From != account && transfer.To != account) continue;
                    if (transfer.TokenId.Length == 0) continue;
                    var key = (notification.ScriptHash, transfer.TokenId);
                    if (updateIndexes.ContainsKey(key)) continue;
                    updateIndexes.Add(key, blockIndex);
                    if (updateIndexes.Count == tokens.Count) break;
                }
            }

            var jsonBalances = new JArray();
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

                jsonBalances.Add(new JObject
                {
                    ["assethash"] = asset.ToString(),
                    ["tokens"] = jsonTokens,
                });
            }

            return new JObject
            {
                ["address"] = AsAddress(account),
                ["balance"] = jsonBalances,
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

            using var engine = ApplicationEngine.Run(builder.ToArray(), neoSystem.StoreView, settings: neoSystem.Settings);

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

        const string TRANSFER = "Transfer";
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
            var account = AsScriptHash(@params[0]);
            ulong startTime = @params.Count > 1
                ? (ulong)@params[1].AsNumber()
                : (DateTime.UtcNow - TimeSpan.FromDays(7)).ToTimestampMS();
            ulong endTime = @params.Count > 2
                ? (ulong)@params[2].AsNumber()
                : DateTime.UtcNow.ToTimestampMS();

            if (endTime < startTime) throw new RpcException(-32602, "Invalid params");

            // iterate over the notifications to populate the send and receive arrays
            var jsonSent = new JArray();
            var jsonReceived = new JArray();

            using var snapshot = neoSystem.GetSnapshot();

            var contracts = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == standard)
                .Select(c => c.scriptHash)
                .ToHashSet();
            var notifications = GetNotifications(SeekDirection.Forward, contracts, TRANSFER);

            foreach (var (blockIndex, txIndex, notification) in notifications)
            {
                var header = Ledger.GetHeader(snapshot, blockIndex);
                if (startTime > header.Timestamp || header.Timestamp > endTime) continue;

                var transfer = TransferNotificationRecord.Create(notification);
                if (transfer is null) continue;
                if (transfer.From != account && transfer.To != account) continue;

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
                if (account == transfer.From)
                {
                    jsonTransfer["transferaddress"] = transfer.To.ToString();
                    jsonSent.Add(jsonTransfer);
                }
                if (account == transfer.To)
                {
                    jsonTransfer["transferaddress"] = transfer.From.ToString();
                    jsonReceived.Add(jsonTransfer);
                }
            }

            return new JObject
            {
                ["address"] = AsAddress(account),
                ["sent"] = jsonSent,
                ["received"] = jsonReceived,
            };
        }

        string AsAddress(UInt160 scriptHash) => Neo.Wallets.Helper.ToAddress(scriptHash, chain.AddressVersion);

        UInt160 AsScriptHash(JObject json)
        {
            var text = json.AsString();
            return text.Length < 40
               ? Neo.Wallets.Helper.ToScriptHash(text, chain.AddressVersion)
               : UInt160.Parse(text);
        }

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
    }
}