using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Node
{

    internal class OnlineNode : IExpressNode
    {
        private readonly ExpressChain chain;
        private readonly RpcClient rpcClient;

        public OnlineNode(ExpressChain chain, ExpressConsensusNode node)
        {
            this.chain = chain;
            rpcClient = new RpcClient(node.GetUri());
        }

        public void Dispose()
        {
        }

        public async Task<UInt256> ExecuteAsync(ExpressWalletAccount account, Script script, decimal additionalGas = 0)
        {
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = account.AsUInt160() } };
            var factory = new TransactionManagerFactory(rpcClient);
            var tm = await factory.MakeTransactionAsync(script, signers).ConfigureAwait(false);
            var tx = await tm
                .AddGas(additionalGas)
                .AddSignatures(chain, account)
                .SignAsync()
                .ConfigureAwait(false);

            return await SubmitTransactionAsync(tx).ConfigureAwait(false);
        }

        public Task<UInt256> SubmitTransactionAsync(Transaction tx)
        {
            return rpcClient.SendRawTransactionAsync(tx);
        }

        public async Task<(Neo.Network.RPC.Models.RpcNep17Balance balance, Nep17Contract contract)[]> GetBalancesAsync(UInt160 address)
        {
            var contracts = ((Neo.IO.Json.JArray)await rpcClient.RpcSendAsync("expressgetnep17contracts"))
                .Select(json => Nep17Contract.FromJson(json))
                .ToDictionary(c => c.ScriptHash);
            var balances = await rpcClient.GetNep17BalancesAsync(address.ToAddress()).ConfigureAwait(false);
            return balances.Balances
                .Select(b => (
                    balance: b,
                    contract: contracts.TryGetValue(b.AssetHash, out var value)
                        ? value
                        : Nep17Contract.Unknown(b.AssetHash)))
                .ToArray();
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

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash)
        {
            return rpcClient.GetTransactionHeightAsync(txHash.ToString());
        }

        public async Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(UInt160 scriptHash)
        {
            var json = await rpcClient.RpcSendAsync("expressgetcontractstorage", scriptHash.ToString())
                .ConfigureAwait(false);

            if (json != null && json is Neo.IO.Json.JArray array)
            {
                return array.Select(s => new ExpressStorage()
                {
                    Key = s["key"].AsString(),
                    Value = s["value"].AsString(),
                    Constant = s["constant"].AsBoolean()
                })
                    .ToList();
            }

            return Array.Empty<ExpressStorage>();
        }

        public async Task<(Transaction tx, Neo.Network.RPC.Models.RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
        {
            var hash = txHash.ToString();
            var response = await rpcClient.GetRawTransactionAsync(hash).ConfigureAwait(false);
            var log = await rpcClient.GetApplicationLogAsync(hash).ConfigureAwait(false);
            return (response.Transaction, log);
        }

        public async Task<RpcInvokeResult> InvokeAsync(Script script)
        {
            return await rpcClient.InvokeScriptAsync(script).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslistcontracts").ConfigureAwait(false);

            if (json != null && json is Neo.IO.Json.JArray array)
            {
                return array
                    .Select(j => (
                        UInt160.Parse(j["hash"].AsString()),
                        ContractManifest.FromJson(j["manifest"])))
                    .ToList();
            }

            return Array.Empty<(UInt160 hash, ContractManifest manifest)>();
        }

        public async Task<IReadOnlyList<Nep17Contract>> ListNep17ContractsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expressgetnep17contracts").ConfigureAwait(false);

            if (json != null && json is Neo.IO.Json.JArray array)
            {
                return array.Select(Nep17Contract.FromJson).ToList();
            }

            return Array.Empty<Nep17Contract>();
        }

        public async Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            var json = await rpcClient.RpcSendAsync("expresslistoraclerequests").ConfigureAwait(false);

            if (json != null && json is Neo.IO.Json.JArray array)
            {
                return array.Select(FromJson).ToList();
            }
            return Array.Empty<(ulong, OracleRequest)>();

            (ulong, OracleRequest) FromJson(Neo.IO.Json.JObject json)
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

        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, ECPoint[] oracleNodes)
        {
            var jsonTx = await rpcClient.RpcSendAsync("expresscreateoracleresponsetx", response.ToJson()).ConfigureAwait(false);
            var tx = Convert.FromBase64String(jsonTx.AsString()).AsSerializable<Transaction>();
            ExpressOracle.SignOracleResponseTransaction(chain, tx, oracleNodes);
            return await SubmitTransactionAsync(tx);
        }
    }
}
