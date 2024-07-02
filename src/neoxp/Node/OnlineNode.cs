// Copyright (C) 2015-2024 The Neo Project.
//
// OnlineNode.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Commands;
using NeoExpress.Models;
using System.Numerics;

namespace NeoExpress.Node
{
    class OnlineNode : IExpressNode
    {
        readonly ExpressChain chain;
        readonly RpcClient rpcClient;
        readonly Lazy<KeyPair[]> consensusNodesKeys;

        public ProtocolSettings ProtocolSettings { get; }

        public OnlineNode(ProtocolSettings settings, ExpressChain chain, ExpressConsensusNode node)
        {
            this.ProtocolSettings = settings;
            this.chain = chain;
            rpcClient = new RpcClient(new Uri($"http://localhost:{node.RpcPort}"), protocolSettings: settings);
            consensusNodesKeys = new Lazy<KeyPair[]>(() => chain.GetConsensusNodeKeys());
        }

        public void Dispose()
        {
        }

        public async Task<IExpressNode.CheckpointMode> CreateCheckpointAsync(string checkPointPath)
        {
            await rpcClient.RpcSendAsync("expresscreatecheckpoint", checkPointPath).ConfigureAwait(false);
            return IExpressNode.CheckpointMode.Online;
        }

        public Task<RpcInvokeResult> InvokeAsync(Script script, Signer? signer = null)
        {
            return signer is null
                ? rpcClient.InvokeScriptAsync(script)
                : rpcClient.InvokeScriptAsync(script, signer);
        }

        public async Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta)
        {
            var prevHash = await rpcClient.GetBestBlockHashAsync().ConfigureAwait(false);
            var prevHeaderHex = await rpcClient.GetBlockHeaderHexAsync($"{prevHash}").ConfigureAwait(false);
            var prevHeader = Convert.FromBase64String(prevHeaderHex).AsSerializable<Header>();

            await NodeUtility.FastForwardAsync(prevHeader,
                blockCount,
                timestampDelta,
                consensusNodesKeys.Value,
                ProtocolSettings.Network,
                block => rpcClient.SubmitBlockAsync(block.ToArray()));
        }

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0)
        {
            var signers = new[] { new Signer { Account = accountHash, Scopes = witnessScope } };
            var tm = await TransactionManager.MakeTransactionAsync(rpcClient, script, signers).ConfigureAwait(false);

            if (additionalGas > 0.0m)
            {
                tm.Tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }

            var account = wallet.GetAccount(accountHash) ?? throw new Exception();
            if (account.IsMultiSigContract())
            {
                var signatureCount = account.Contract.ParameterList.Length;
                var multiSigWallets = chain.GetMultiSigWallets(ProtocolSettings, accountHash);
                if (multiSigWallets.Count < signatureCount)
                    throw new InvalidOperationException();

                var publicKeys = multiSigWallets
                    .Select(w => (w.GetAccount(accountHash)?.GetKey() ?? throw new Exception()).PublicKey)
                    .ToArray();

                for (var i = 0; i < signatureCount; i++)
                {
                    var key = multiSigWallets[i].GetAccount(accountHash)?.GetKey() ?? throw new Exception();
                    tm.AddMultiSig(key, signatureCount, publicKeys);
                }
            }
            else
            {
                tm.AddSignature(account.GetKey() ?? throw new Exception());
            }

            var tx = await tm.SignAsync().ConfigureAwait(false);

            return await rpcClient.SendRawTransactionAsync(tx).ConfigureAwait(false);
        }

        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes)
        {
            var jsonTx = await rpcClient.RpcSendAsync("expresscreateoracleresponsetx", response.ToJson()).ConfigureAwait(false);
            var tx = Convert.FromBase64String(jsonTx.AsString()).AsSerializable<Transaction>();
            NodeUtility.SignOracleResponseTransaction(ProtocolSettings, chain, tx, oracleNodes);

            return await rpcClient.SendRawTransactionAsync(tx).ConfigureAwait(false);
        }

        public async Task<Block> GetBlockAsync(UInt256 blockHash)
        {
            var rpcBlock = await rpcClient.GetBlockAsync(blockHash.ToString()).ConfigureAwait(false);
            return rpcBlock.Block;
        }

        public async Task<Block> GetBlockAsync(uint blockIndex)
        {
            var rpcBlock = await rpcClient.GetBlockAsync(blockIndex.ToString()).ConfigureAwait(false);
            return rpcBlock.Block;
        }

        public async Task<ContractManifest> GetContractAsync(UInt160 scriptHash)
        {
            var contractState = await rpcClient.GetContractStateAsync(scriptHash.ToString()).ConfigureAwait(false);
            return contractState.Manifest;
        }

        public async Task<Block> GetLatestBlockAsync()
        {
            var hash = await rpcClient.GetBestBlockHashAsync().ConfigureAwait(false);
            var rpcBlock = await rpcClient.GetBlockAsync(hash).ConfigureAwait(false);
            return rpcBlock.Block;
        }

        public async Task<(Transaction tx, Neo.Network.RPC.Models.RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
        {
            var hash = txHash.ToString();
            var response = await rpcClient.GetRawTransactionAsync(hash).ConfigureAwait(false);
            var log = await rpcClient.GetApplicationLogAsync(hash).ConfigureAwait(false);
            return (response.Transaction, log);
        }

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash)
        {
            return rpcClient.GetTransactionHeightAsync(txHash.ToString());
        }

        public async Task<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address)
        {
            var rpcBalances = await rpcClient.GetNep17BalancesAsync(address.ToAddress(ProtocolSettings.AddressVersion))
                .ConfigureAwait(false);
            var balanceMap = rpcBalances.Balances.ToDictionary(b => b.AssetHash, b => b.Amount);
            var contracts = await ListTokenContractsAsync().ConfigureAwait(false);
            return contracts.Select(c => (c, balanceMap.TryGetValue(c.ScriptHash, out var value) ? value : 0)).ToList();
        }

        public async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslistcontracts").ConfigureAwait(false);

            if (json is not null && json is JArray array)
            {
                return array
                    .Select(j => (
                        UInt160.Parse(j!["hash"]!.AsString()),
                        ContractManifest.FromJson((JObject)j["manifest"]!)))
                    .ToList();
            }

            return Array.Empty<(UInt160 hash, ContractManifest manifest)>();
        }

        public async Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslisttokencontracts").ConfigureAwait(false);

            if (json is not null && json is JArray array)
            {
                return array.Select(j => TokenContract.FromJson(j!)).ToList();
            }

            return Array.Empty<TokenContract>();
        }

        public async Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslistoraclerequests").ConfigureAwait(false);

            if (json is not null && json is JArray array)
            {
                return array.Select(j => FromJson(j!)).ToList();
            }
            return Array.Empty<(ulong, OracleRequest)>();

            (ulong, OracleRequest) FromJson(JToken json)
            {
                var id = ulong.Parse(json["requestid"]!.AsString());
                var originalTxId = UInt256.Parse(json["originaltxid"]!.AsString());
                var gasForResponse = long.Parse(json["gasforresponse"]!.AsString());
                var url = json["url"]!.AsString();
                var filter = json["filter"]!.AsString();
                var callbackContract = UInt160.Parse(json["callbackcontract"]!.AsString());
                var callbackMethod = json["callbackmethod"]!.AsString();
                var userData = Convert.FromBase64String(json["userdata"]!.AsString());

                return (id, new OracleRequest
                {
                    OriginalTxid = originalTxId,
                    CallbackContract = callbackContract,
                    CallbackMethod = callbackMethod,
                    Filter = filter,
                    GasForResponse = gasForResponse,
                    Url = url,
                    UserData = userData,
                });
            }
        }

        public async Task<IReadOnlyList<(string key, string value)>> ListStoragesAsync(UInt160 scriptHash)
        {
            var json = await rpcClient.RpcSendAsync("expressgetcontractstorage", scriptHash.ToString())
                .ConfigureAwait(false);

            if (json is not null && json is JArray array)
            {
                return array
                    .Select(s => (s!["key"]!.AsString(), s!["value"]!.AsString()))
                    .ToList();
            }

            return Array.Empty<(string, string)>();
        }

        public async Task<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Contract download is only supported for single-node consensus");
            }

            JObject o = new JObject();
            o["state"] = state.ToJson();

            JArray storage = new JArray();
            foreach (var pair in storagePairs)
            {
                JObject kv = new JObject();
                kv["key"] = pair.key;
                kv["value"] = pair.value;
                storage.Add(kv);
            }

            o["storage"] = storage;
            o["force"] = force;

            var response = await rpcClient.RpcSendAsync("expresspersistcontract", o).ConfigureAwait(false);
            return (int)response.AsNumber();
        }

        public async Task<int> PersistStorageKeyValueAsync(UInt160 scripthash, (string key, string value) storagePair)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Contract storage update is only supported for single-node consensus");
            }

            var state = await rpcClient.GetContractStateAsync(scripthash.ToString()).ConfigureAwait(false);

            JObject o = new JObject();
            o["state"] = state.ToJson();

            JArray storage = new JArray();
            JObject kv = new JObject();
            kv["key"] = storagePair.key;
            kv["value"] = storagePair.value;
            storage.Add(kv);

            o["storage"] = storage;

            var response = await rpcClient.RpcSendAsync("expresspersiststorage", o).ConfigureAwait(false);
            return (int)response.AsNumber();
        }

        public async IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter)
        {
            var contractsArg = new JArray((contractFilter ?? Enumerable.Empty<UInt160>())
                .Select(c => new JString($"{c}")));
            var eventsArg = new JArray((eventFilter ?? Enumerable.Empty<string>())
                .Select(e => new JString(e)));

            var count = 0;
            while (true)
            {
                var json = await rpcClient.RpcSendAsync("expressenumnotifications", contractsArg, eventsArg, count, 3)
                    .ConfigureAwait(false);

                var truncated = json["truncated"]!.AsBoolean();
                var values = (JArray)json["notifications"]!;

                foreach (var v in values)
                {
                    var blockIndex = (uint)v!["block-index"]!.AsNumber();
                    var scriptHash = UInt160.Parse(v["script-hash"]!.AsString());
                    var eventName = v["event-name"]!.AsString();
                    var invType = (InventoryType)(byte)v["inventory-type"]!.AsNumber();
                    var invHash = UInt256.Parse(v["inventory-hash"]!.AsString());
                    var state = (Neo.VM.Types.Array)Neo.Network.RPC.Utility.StackItemFromJson((JObject)v["state"]!);
                    var notification = new NotificationRecord(scriptHash, eventName, state, invType, invHash);
                    yield return (blockIndex, notification);
                }

                if (!truncated)
                    break;

                count += values.Count;
            }
        }

        public async Task<bool> IsNep17CompliantAsync(UInt160 contractHash)
        {
            var json = await rpcClient.RpcSendAsync("expressisnep17compliant", contractHash.ToString()).ConfigureAwait(false);

            if (json != null && json is JBoolean)
                return json.AsBoolean();
            return false;
        }

        public async Task<bool> IsNep11CompliantAsync(UInt160 contractHash)
        {
            var json = await rpcClient.RpcSendAsync("expressisnep11compliant", contractHash.ToString()).ConfigureAwait(false);

            if (json != null && json is JBoolean)
                return json.AsBoolean();
            return false;
        }
    }
}
