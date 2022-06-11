using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Akka.Actor;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
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
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    sealed class OfflineNode : IExpressNode
    {
        readonly ExpressSystem expressSystem;

        public ProtocolSettings ProtocolSettings => expressSystem.ProtocolSettings;

        public OfflineNode(IExpressChain chain, RocksDbExpressStorage expressStorage, bool enableTrace)
        {
            var node = chain.ConsensusNodes[0];
            expressSystem = new ExpressSystem(chain, node, expressStorage, enableTrace, null);
        }

        public void Dispose() => expressSystem.Dispose();

        public ValueTask<IExpressNode.CheckpointMode> CreateCheckpointAsync(string path)
            => MakeAsync(() =>
                {
                    expressSystem.CreateCheckpoint(path);
                    return IExpressNode.CheckpointMode.Offline;
                });

        public ValueTask<RpcInvokeResult> InvokeAsync(Script script, Signer? signer = null)
            => MakeAsync(() => expressSystem.Invoke(script, signer));

        public Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0)
            => expressSystem.ExecuteAsync(wallet, accountHash, witnessScope, script, additionalGas);

        public Task<UInt256> SubmitOracleResponseAsync(OracleResponse response)
            => expressSystem.SubmitOracleResponseAsync(response);

        public Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta)
            => expressSystem.FastForwardAsync(blockCount, timestampDelta);

        public ValueTask<Block> GetBlockAsync(UInt256 blockHash)
            => MakeAsync(() => expressSystem.GetBlock(blockHash));

        public ValueTask<Block> GetBlockAsync(uint blockIndex)
            => MakeAsync(() => expressSystem.GetBlock(blockIndex));

        public ValueTask<Block> GetLatestBlockAsync()
            => MakeAsync(() => expressSystem.GetLatestBlock());

        public ValueTask<ContractManifest> GetContractAsync(UInt160 scriptHash)
            => MakeAsync(() => expressSystem.GetContract(scriptHash));

        public ValueTask<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
            => MakeAsync(() => 
                {
                    var tx = expressSystem.GetTransaction(txHash);
                    var appLog = expressSystem.GetAppLog(txHash);
                    return (tx, appLog);
                });

        public ValueTask<uint> GetTransactionHeightAsync(UInt256 txHash)
            => MakeAsync(() => expressSystem.GetTransactionHeight(txHash));

        public ValueTask<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address)
            => MakeAsync(() => expressSystem.ListBalances(address));

        public ValueTask<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
            => MakeAsync(() => expressSystem.ListContracts().ToReadOnlyList());

        public ValueTask<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
            => MakeAsync(() => expressSystem.ListOracleRequests().ToReadOnlyList());

        public ValueTask<IReadOnlyList<(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value)>> ListStoragesAsync(UInt160 scriptHash)
            => MakeAsync(() =>  expressSystem.ListStorages(scriptHash).ToReadOnlyList());

        public ValueTask<IReadOnlyList<TokenContract>> ListTokenContractsAsync()
            => MakeAsync(() => expressSystem.ListTokenContracts().ToReadOnlyList());

        public ValueTask<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force)
            => MakeAsync(() => expressSystem.PersistContract(state, storagePairs, force));

        public async IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter)
        {
            var notifications = expressSystem.GetNotifications(SeekDirection.Backward, contractFilter, eventFilter);
            foreach (var (block, _, notification) in notifications)
            {
                yield return (block, notification);
            }
        }

        ValueTask<T> MakeAsync<T>(Func<T> func)
        {
            try
            {
                return ValueTask.FromResult(func());
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<T>(ex);
            }
        }
    }
}
