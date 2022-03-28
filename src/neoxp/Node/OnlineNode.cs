using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Commands;
using NeoExpress.Models;

namespace NeoExpress.Node
{

    internal class OnlineNode : IExpressNode
    {
        private readonly ExpressChain chain;
        private readonly RpcClient rpcClient;

        public ProtocolSettings ProtocolSettings { get; }

        public OnlineNode(ProtocolSettings settings, ExpressChain chain, ExpressConsensusNode node)
        {
            this.ProtocolSettings = settings;
            this.chain = chain;
            rpcClient = new RpcClient(new Uri($"http://localhost:{node.RpcPort}"), protocolSettings: settings);
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
            return signer == null
                ? rpcClient.InvokeScriptAsync(script)
                : rpcClient.InvokeScriptAsync(script, signer);
        }

        // Need an IVerifiable.GetScriptHashesForVerifying implementation that doesn't
        // depend on the DataCache snapshot parameter since the ContractParametersContext
        // we are creating here does not have direct access to node data.

        class BlockScriptHashes : IVerifiable
        {
            readonly UInt160[] hashes;

            public BlockScriptHashes(UInt160 scriptHash)
            {
                hashes = new[] { scriptHash };
            }

            public UInt160[] GetScriptHashesForVerifying(DataCache snapshot) => hashes;

            Witness[] IVerifiable.Witnesses
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            int ISerializable.Size => throw new NotImplementedException();

            void ISerializable.Deserialize(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            void IVerifiable.DeserializeUnsigned(BinaryReader reader)
            {
                throw new NotImplementedException();
            }

            void ISerializable.Serialize(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }

            void IVerifiable.SerializeUnsigned(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }
        }

        public async Task FastForwardAsync(uint blockCount)
        {
            var keyPairs = chain.ConsensusNodes
                .Select(n =>
                {
                    var account = n.Wallet.DefaultAccount
                        ?? throw new InvalidOperationException($"Invalid default account for {n.Wallet.Name}");
                    return new KeyPair(account.PrivateKey.HexToBytes());
                })
                .ToArray();

            for (var i = 0u; i < blockCount; i++)
            {
                await SubmitEmptyBlockAsync(rpcClient, keyPairs, ProtocolSettings.Network).ConfigureAwait(false);
            }

            static async Task SubmitEmptyBlockAsync(RpcClient rpcClient, IReadOnlyList<KeyPair> keyPairs, uint network)
            {
                var hash = await rpcClient.GetBestBlockHashAsync().ConfigureAwait(false);
                var header = Convert.FromBase64String(await rpcClient.GetBlockHeaderHexAsync($"{hash}"))
                    .AsSerializable<Header>();

                var block = new Block
                {
                    Header = new Header
                    {
                        Version = 0,
                        PrevHash = header.Hash,
                        MerkleRoot = UInt256.Zero,
                        Timestamp = Math.Max(Neo.Helper.ToTimestampMS(DateTime.UtcNow), header.Timestamp + 1),
                        Index = header.Index + 1,
                        PrimaryIndex = 0,
                        NextConsensus = header.NextConsensus,
                    },
                    Transactions = Array.Empty<Transaction>()
                };

                var validators = (await rpcClient.GetNextBlockValidatorsAsync().ConfigureAwait(false))
                        .Select(v => ECPoint.Parse(v.PublicKey, ECCurve.Secp256r1)).ToArray();
                var signatureCount = validators.Length - (validators.Length - 1) / 3;
                var contract = Contract.CreateMultiSigContract(signatureCount, validators);

                var context = new ContractParametersContext(null, new BlockScriptHashes(header.NextConsensus), network);
                for (int i = 0; i < keyPairs.Count; i++)
                {
                    var signature = block.Sign(keyPairs[i], network);
                    context.AddSignature(contract, keyPairs[i].PublicKey, signature);
                    if (context.Completed) break;
                }
                if (!context.Completed) throw new InvalidOperationException("Invalid block signature");
                block.Header.Witness = context.GetWitnesses().Single();

                await rpcClient.SubmitBlockAsync(block.ToArray()).ConfigureAwait(false);
            }
        }

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0)
        {
            var signers = new[] { new Signer { Account = accountHash, Scopes = witnessScope } };
            var factory = new TransactionManagerFactory(rpcClient);
            var tm = await factory.MakeTransactionAsync(script, signers).ConfigureAwait(false);

            if (additionalGas > 0.0m)
            {
                tm.Tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }

            var account = wallet.GetAccount(accountHash) ?? throw new Exception();
            if (account.Contract.Script.IsMultiSigContract())
            {
                var signatureCount = account.Contract.ParameterList.Length;
                var multiSigWallets = chain.GetMultiSigWallets(ProtocolSettings, accountHash);
                if (multiSigWallets.Count < signatureCount) throw new InvalidOperationException();

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
            var contracts = (await ListTokenContractsAsync().ConfigureAwait(false))
                .ToDictionary(c => c.ScriptHash);
            var rpcBalances = await rpcClient.GetNep17BalancesAsync(address.ToAddress(ProtocolSettings.AddressVersion))
                .ConfigureAwait(false);

            return rpcBalances.Balances.Select(b => (contracts[b.AssetHash], b.Amount)).ToList();
        }

        public async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslistcontracts").ConfigureAwait(false);

            if (json != null && json is JArray array)
            {
                return array
                    .Select(j => (
                        UInt160.Parse(j["hash"].AsString()),
                        ContractManifest.FromJson(j["manifest"])))
                    .ToList();
            }

            return Array.Empty<(UInt160 hash, ContractManifest manifest)>();
        }

        public async Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslisttokencontracts").ConfigureAwait(false);

            if (json != null && json is JArray array)
            {
                return array.Select(TokenContract.FromJson).ToList();
            }

            return Array.Empty<TokenContract>();
        }

        public async Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslistoraclerequests").ConfigureAwait(false);

            if (json != null && json is JArray array)
            {
                return array.Select(FromJson).ToList();
            }
            return Array.Empty<(ulong, OracleRequest)>();

            (ulong, OracleRequest) FromJson(JObject json)
            {
                var id = ulong.Parse(json["requestid"].AsString());
                var originalTxId = UInt256.Parse(json["originaltxid"].AsString());
                var gasForResponse = long.Parse(json["gasforresponse"].AsString());
                var url = json["url"].AsString();
                var filter = json["filter"].AsString();
                var callbackContract = UInt160.Parse(json["callbackcontract"].AsString());
                var callbackMethod = json["callbackmethod"].AsString();
                var userData = Convert.FromBase64String(json["userdata"].AsString());

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

        public async Task<IReadOnlyList<ExpressStorage>> ListStoragesAsync(UInt160 scriptHash)
        {
            var json = await rpcClient.RpcSendAsync("expressgetcontractstorage", scriptHash.ToString())
                .ConfigureAwait(false);

            if (json != null && json is JArray array)
            {
                return array.Select(s => new ExpressStorage()
                {
                    Key = s["key"].AsString(),
                    Value = s["value"].AsString(),
                })
                    .ToList();
            }

            return Array.Empty<ExpressStorage>();
        }

        public async Task<int> PersistContractAsync(ContractState state, (string key, string value)[] storagePairs, ContractCommand.ContractForce? force)
        {
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
            o["force"] = force is null ? JObject.Null : force;
            
            var response = await rpcClient.RpcSendAsync("expresspersistcontract", o).ConfigureAwait(false);
            return (int)response.AsNumber();
        }

        public async IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter)
        {
            JObject contractsArg = (contractFilter ?? Enumerable.Empty<UInt160>())
                .Select(c => (JString)c.ToString())
                .ToArray();
            JObject eventsArg = (eventFilter ?? Enumerable.Empty<string>())
                .Select(e => (JString)e)
                .ToArray();

            var count = 0;

            while (true)
            {
                var json = await rpcClient.RpcSendAsync("expressenumnotifications", contractsArg, eventsArg, count, 3)
                    .ConfigureAwait(false);

                var truncated = json["truncated"].AsBoolean();
                var values = (JArray)json["notifications"];

                foreach (var v in values)
                {
                    var blockIndex = (uint)v["block-index"].AsNumber();
                    var scriptHash = UInt160.Parse(v["script-hash"].AsString());
                    var eventName = v["event-name"].AsString();
                    var invType = (InventoryType)(byte)v["inventory-type"].AsNumber();
                    var invHash = UInt256.Parse(v["inventory-hash"].AsString());
                    var state = (Neo.VM.Types.Array)Neo.Network.RPC.Utility.StackItemFromJson(v["state"]);
                    var notification = new NotificationRecord(scriptHash, eventName, state, invType, invHash);
                    yield return (blockIndex, notification);
                }

                if (!truncated) break;

                count += values.Count;
            }
        }
    }
}
