using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Akka.Actor;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
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
    internal sealed class OfflineNode : IDisposable, IExpressNode
    {
        readonly NeoSystem neoSystem;
        readonly ApplicationEngineProvider? applicationEngineProvider;
        readonly Wallet nodeWallet;
        readonly RocksDbStorageProvider rocksDbStorageProvider;
        readonly Lazy<KeyPair[]> consensusNodesKeys;
        bool disposedValue;

        public IExpressFile ExpressFile { get; }
        public ProtocolSettings ProtocolSettings => neoSystem.Settings;

        public OfflineNode(
            IExpressFile expressFile,
            ExpressConsensusNode node,
            RocksDbStorageProvider rocksDbStorageProvider, 
            bool enableTrace)
        {
            this.ExpressFile = expressFile;
            var settings = expressFile.Chain.GetProtocolSettings();
            this.nodeWallet = DevWallet.FromExpressWallet(settings, node.Wallet);
            this.rocksDbStorageProvider = rocksDbStorageProvider;
            applicationEngineProvider = enableTrace ? new ApplicationEngineProvider() : null;
            consensusNodesKeys = new Lazy<KeyPair[]>(() => expressFile.Chain.GetConsensusNodeKeys());

            var storageProviderPlugin = new StorageProviderPlugin(rocksDbStorageProvider);
            _ = new PersistencePlugin(rocksDbStorageProvider);
            neoSystem = new NeoSystem(settings, storageProviderPlugin.Name);

            ApplicationEngine.Log += OnLog!;
        }

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    neoSystem.Dispose();
                    rocksDbStorageProvider.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            var engine = sender as ApplicationEngine;
            var tx = engine?.ScriptContainer as Transaction;
            var colorCode = tx?.Witnesses?.Any() ?? false ? "96" : "93";

            var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, args.ScriptHash);
            var name = contract == null ? args.ScriptHash.ToString() : contract.Manifest.Name;
            Console.WriteLine($"\x1b[35m{name}\x1b[0m Log: \x1b[{colorCode}m\"{args.Message}\"\x1b[0m [{args.ScriptContainer.GetType().Name}]");
        }

        Task<T> MakeAsync<T>(Func<T> func)
        {
            try
            {
                if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        IExpressNode.CheckpointMode CreateCheckpoint(string checkPointPath)
        {
            var multiSigAccount = nodeWallet.GetMultiSigAccounts().Single();
            rocksDbStorageProvider.CreateCheckpoint(checkPointPath, ProtocolSettings, multiSigAccount.ScriptHash);
            return IExpressNode.CheckpointMode.Offline;
        }

        public Task<IExpressNode.CheckpointMode> CreateCheckpointAsync(string checkPointPath)
            => MakeAsync(() => CreateCheckpoint(checkPointPath));

        RpcInvokeResult Invoke(Neo.VM.Script script, Signer? signer = null)
        {
            var tx = TestApplicationEngine.CreateTestTransaction(signer);
            using var engine = script.Invoke(neoSystem.Settings, neoSystem.StoreView, tx);

            return new RpcInvokeResult()
            {
                State = engine.State,
                Exception = engine.FaultException?.GetBaseException().Message ?? string.Empty,
                GasConsumed = engine.GasConsumed,
                Stack = engine.ResultStack.ToArray(),
                Script = string.Empty,
                Tx = string.Empty
            };
        }

        public Task<RpcInvokeResult> InvokeAsync(Neo.VM.Script script, Signer? signer = null)
            => MakeAsync(() => Invoke(script, signer));

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Neo.VM.Script script, decimal additionalGas = 0)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var signer = new Signer() { Account = accountHash, Scopes = witnessScope };
            var (balance, _) = await this.GetBalanceAsync(accountHash, "GAS");
            var tx = wallet.MakeTransaction(neoSystem.StoreView, script, accountHash, new[] { signer }, maxGas: (long)balance.Amount);
            if (additionalGas > 0.0m)
            {
                tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }

            var context = new ContractParametersContext(neoSystem.StoreView, tx, ProtocolSettings.Network);
            var account = wallet.GetAccount(accountHash) ?? throw new Exception();
            if (account.IsMultiSigContract())
            {
                var multiSigWallets = ExpressFile.Chain.GetMultiSigWallets(neoSystem.Settings, accountHash);
                for (int i = 0; i < multiSigWallets.Count; i++)
                {
                    multiSigWallets[i].Sign(context);
                    if (context.Completed) break;
                }
            }
            else
            {
                wallet.Sign(context);
            }

            if (!context.Completed)
            {
                throw new Exception();
            }

            tx.Witnesses = context.GetWitnesses();
            var blockHash = await SubmitTransactionAsync(tx).ConfigureAwait(false);
            return tx.Hash;
        }

        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();
            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings);
            if (tx == null) throw new Exception("Failed to create Oracle Response Tx");
            NodeUtility.SignOracleResponseTransaction(ProtocolSettings, ExpressFile.Chain, tx, oracleNodes);

            var blockHash = await SubmitTransactionAsync(tx);
            return tx.Hash;
        }

        public async Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var prevHash = NativeContract.Ledger.CurrentHash(neoSystem.StoreView);
            var prevHeader = NativeContract.Ledger.GetHeader(neoSystem.StoreView, prevHash);

            await NodeUtility.FastForwardAsync(prevHeader,
                blockCount,
                timestampDelta,
                consensusNodesKeys.Value,
                ProtocolSettings.Network,
                block => RelayBlockAsync(block));
        }

        async Task<UInt256> SubmitTransactionAsync(Transaction tx)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var transactions = new[] { tx };

            // Verify the provided transactions. When running, Blockchain class does verification in two steps: VerifyStateIndependent and VerifyStateDependent.
            // However, Verify does both parts and there's no point in verifying dependent/independent in separate steps here
            var verificationContext = new TransactionVerificationContext();
            for (int i = 0; i < transactions.Length; i++)
            {
                if (transactions[i].Verify(neoSystem.Settings, neoSystem.StoreView, verificationContext) != VerifyResult.Succeed)
                {
                    throw new Exception("Verification failed");
                }
            }

            var prevHash = NativeContract.Ledger.CurrentHash(neoSystem.StoreView);
            var prevHeader = NativeContract.Ledger.GetHeader(neoSystem.StoreView, prevHash);
            var block = NodeUtility.CreateSignedBlock(prevHeader,
                consensusNodesKeys.Value,
                neoSystem.Settings.Network,
                transactions);
            await RelayBlockAsync(block).ConfigureAwait(false);
            return block.Hash;
        }

        async Task RelayBlockAsync(Block block)
        {
            var blockRelay = await neoSystem.Blockchain.Ask<RelayResult>(block).ConfigureAwait(false);
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }
        }

        (Transaction tx, RpcApplicationLog? appLog) GetTransaction(UInt256 txHash)
        {
            var tx = NativeContract.Ledger.GetTransaction(neoSystem.StoreView, txHash);
            if (tx == null) throw new Exception("Unknown Transaction");

            var jsonLog = PersistencePlugin.GetAppLog(rocksDbStorageProvider, txHash);
            return jsonLog != null
                ? (tx, RpcApplicationLog.FromJson(jsonLog, ProtocolSettings))
                : (tx, null);
        }

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
            => MakeAsync(() => GetTransaction(txHash));

        IReadOnlyList<(TokenContract contract, BigInteger balance)> ListBalances(UInt160 address)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var contracts = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == TokenStandard.Nep17)
                .ToList();

            var addressArray = address.ToArray();
            using var builder = new ScriptBuilder();
            for (var i = contracts.Count; i-- > 0;)
            {
                builder.EmitDynamicCall(contracts[i].scriptHash, "symbol");
                builder.EmitDynamicCall(contracts[i].scriptHash, "decimals");
                builder.EmitDynamicCall(contracts[i].scriptHash, "balanceOf", addressArray);
            }

            List<(TokenContract contract, BigInteger balance)> balances = new();
            using var engine = builder.Invoke(neoSystem.Settings, snapshot);
            if (engine.State != VMState.FAULT && engine.ResultStack.Count == contracts.Count * 3)
            {
                var resultStack = engine.ResultStack;
                for (var i = 0; i < contracts.Count; i++)
                {
                    var index = i * 3;
                    var symbol = resultStack.Peek(index + 2).GetString();
                    if (symbol == null) continue;
                    var decimals = (byte)resultStack.Peek(index + 1).GetInteger();
                    var balance = resultStack.Peek(index).GetInteger();
                    balances.Add((new TokenContract(symbol, decimals, contracts[i].scriptHash, contracts[i].standard), balance));
                }
            }

            return balances;
        }

        public Task<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address)
            => MakeAsync(() => ListBalances(address));

        IReadOnlyList<(UInt160 hash, ContractManifest manifest)> ListContracts()
        {
            return NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
                .OrderBy(c => c.Id)
                .Select(c => (c.Hash, c.Manifest))
                .ToList();
        }

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
            => MakeAsync(ListContracts);

        IReadOnlyList<(ulong requestId, OracleRequest request)> ListOracleRequests()
            => NativeContract.Oracle.GetRequests(neoSystem.StoreView).ToList();

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
            => MakeAsync(ListOracleRequests);

        IReadOnlyList<TokenContract> ListTokenContracts()
        {
            using var snapshot = neoSystem.GetSnapshot();
            return snapshot.EnumerateTokenContracts(neoSystem.Settings).ToList();
        }

        public Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync()
            => MakeAsync(ListTokenContracts);

        IReadOnlyList<(string key, string value)> ListStorages(UInt160 scriptHash)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);

            if (contract == null) return Array.Empty<(string, string)>();

            byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
            return snapshot.Find(prefix)
                .Select(t => (t.Key.Key.ToHexString(), t.Value.Value.ToHexString()))
                .ToList();
        }

        public Task<IReadOnlyList<(string key, string value)>> ListStoragesAsync(UInt160 scriptHash)
            => MakeAsync(() => ListStorages(scriptHash));

        public Task<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force)
            => MakeAsync(() =>
            {
                if (ExpressFile.Chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Contract download is only supported for single-node consensus");
                }

                return NodeUtility.PersistContract(neoSystem, state, storagePairs, force);
            });

        // warning CS1998: This async method lacks 'await' operators and will run synchronously.
        // EnumerateNotificationsAsync has to be async in order to be polymorphic with OnlineNode's implementation
#pragma warning disable 1998 
        public async IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter)
        {
            var notifications = PersistencePlugin.GetNotifications(this.rocksDbStorageProvider, SeekDirection.Backward, contractFilter, eventFilter);
            foreach (var (block, _, notification) in notifications)
            {
                yield return (block, notification);
            }
        }
#pragma warning restore 1998
    }
}
