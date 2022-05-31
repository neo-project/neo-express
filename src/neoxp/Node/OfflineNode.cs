using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
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

namespace NeoExpress.Node
{
    class OfflineNode : IDisposable, IExpressNode
    {
        readonly ExpressSystem expressSystem;
        readonly Lazy<KeyPair[]> consensusNodesKeys;
        bool disposedValue;

        public IExpressChain Chain { get; }
        public ProtocolSettings ProtocolSettings => expressSystem.ProtocolSettings;

        public OfflineNode(
            IExpressChain chain,
            ExpressConsensusNode node,
            IExpressStorage expressStorage,
            bool enableTrace)
        {
            ApplicationEngine.Log += OnLog!;

            this.Chain = chain;
            expressSystem = new ExpressSystem(chain, node, expressStorage, enableTrace, null);
            consensusNodesKeys = new Lazy<KeyPair[]>(() => chain.GetConsensusNodeKeys());
        }

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    expressSystem.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

#pragma warning disable 1998
        public async Task<RpcInvokeResult> InvokeAsync(Neo.VM.Script script, Signer? signer = null)
#pragma warning restore 1998
        {
            var tx = TestApplicationEngine.CreateTestTransaction(signer);
            using var engine = script.Invoke(ProtocolSettings, expressSystem.StoreView, tx);

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

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Neo.VM.Script script, decimal additionalGas = 0)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var signer = new Signer() { Account = accountHash, Scopes = witnessScope };
            var (balance, _) = await this.GetBalanceAsync(accountHash, "GAS");
            var tx = wallet.MakeTransaction(expressSystem.StoreView, script, accountHash, new[] { signer }, maxGas: (long)balance.Amount);
            if (additionalGas > 0.0m)
            {
                tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }

            var account = wallet.GetAccount(accountHash) ?? throw new Exception($"{accountHash} not found");

            var context = new ContractParametersContext(expressSystem.StoreView, tx, ProtocolSettings.Network);
            if (account.Contract.Script.IsMultiSigContract())
            {
                var signatureCount = account.Contract.ParameterList.Length;
                var accountWallets = Chain.GetAccountWallets(accountHash);
                if (accountWallets.Count < signatureCount) throw new Exception($"{signatureCount} signatures needed, only {accountWallets.Count} wallets found");

                for (int i = 0; i < accountWallets.Count; i++)
                {
                    accountWallets[i].Sign(context);
                    if (context.Completed) break;
                }
            }
            else
            {
                wallet.Sign(context);
            }

            if (!context.Completed) throw new Exception("Not enough signatures provided");

            tx.Witnesses = context.GetWitnesses();
            var blockHash = await SubmitTransactionAsync(tx).ConfigureAwait(false);
            return tx.Hash;
        }

#pragma warning disable 1998
        public async Task<Block> GetBlockAsync(UInt256 hash)
            => NativeContract.Ledger.GetBlock(expressSystem.StoreView, hash)
                ?? throw new Exception("Unknown block");

        public async Task<Block> GetBlockAsync(uint index)
            => NativeContract.Ledger.GetBlock(expressSystem.StoreView, index)
                ?? throw new Exception("Unknown block");

        public async Task<Block> GetLatestBlockAsync()
        {
            var hash = NativeContract.Ledger.CurrentHash(expressSystem.StoreView)
                ?? throw new Exception("no current hash");
            return NativeContract.Ledger.GetBlock(expressSystem.StoreView, hash)
                ?? throw new Exception("Unknown block");
        }

        public async Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
        {
            var tx = NativeContract.Ledger.GetTransaction(expressSystem.StoreView, txHash)
                ?? throw new Exception("Unknown Transaction");
            var appLog = expressSystem.GetAppLog(txHash);
            return appLog != null
                ? (tx, RpcApplicationLog.FromJson(appLog, ProtocolSettings))
                : (tx, null);
        }

        public async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
            => expressSystem.ListContracts().ToList();
#pragma warning restore 1998








































        void OnLog(object sender, LogEventArgs args)
        {
            var engine = sender as ApplicationEngine;
            var tx = engine?.ScriptContainer as Transaction;
            var colorCode = tx?.Witnesses?.Any() ?? false ? "96" : "93";

            var contract = NativeContract.ContractManagement.GetContract(expressSystem.StoreView, args.ScriptHash);
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
            throw new NotImplementedException();
            // var multiSigAccount = nodeWallet.GetMultiSigAccounts().Single();
            // rocksDbStorageProvider.CreateCheckpoint(checkPointPath, ProtocolSettings, multiSigAccount.ScriptHash);
            // return IExpressNode.CheckpointMode.Offline;
        }

        public Task<IExpressNode.CheckpointMode> CreateCheckpointAsync(string checkPointPath)
            => MakeAsync(() => CreateCheckpoint(checkPointPath));



        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = expressSystem.GetSnapshot();
            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings);
            if (tx == null) throw new Exception("Failed to create Oracle Response Tx");
            // NodeUtility.SignOracleResponseTransaction(ProtocolSettings, ExpressFile.Chain, tx, oracleNodes);

            var blockHash = await SubmitTransactionAsync(tx);
            return tx.Hash;
        }

        public async Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var prevHash = NativeContract.Ledger.CurrentHash(expressSystem.StoreView);
            var prevHeader = NativeContract.Ledger.GetHeader(expressSystem.StoreView, prevHash);

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
                if (transactions[i].Verify(ProtocolSettings, expressSystem.StoreView, verificationContext) != VerifyResult.Succeed)
                {
                    throw new Exception("Verification failed");
                }
            }

            var prevHash = NativeContract.Ledger.CurrentHash(expressSystem.StoreView);
            var prevHeader = NativeContract.Ledger.GetHeader(expressSystem.StoreView, prevHash);
            var block = NodeUtility.CreateSignedBlock(prevHeader,
                consensusNodesKeys.Value,
                ProtocolSettings.Network,
                transactions);
            await RelayBlockAsync(block).ConfigureAwait(false);
            return block.Hash;
        }

        async Task RelayBlockAsync(Block block)
        {
            var blockRelay = await expressSystem.RelayBlockAsync(block).ConfigureAwait(false);
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }
        }


        IReadOnlyList<(TokenContract contract, BigInteger balance)> ListBalances(UInt160 address)
        {
            using var snapshot = expressSystem.GetSnapshot();
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
            using var engine = builder.Invoke(ProtocolSettings, snapshot);
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



        IReadOnlyList<(ulong requestId, OracleRequest request)> ListOracleRequests()
            => NativeContract.Oracle.GetRequests(expressSystem.StoreView).ToList();

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
            => MakeAsync(ListOracleRequests);

        IReadOnlyList<TokenContract> ListTokenContracts()
        {
            using var snapshot = expressSystem.GetSnapshot();
            return snapshot.EnumerateTokenContracts(ProtocolSettings).ToList();
        }

        public Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync()
            => MakeAsync(ListTokenContracts);

        IReadOnlyList<(string key, string value)> ListStorages(UInt160 scriptHash)
        {
            using var snapshot = expressSystem.GetSnapshot();
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
            => MakeAsync<int>(() =>
            {
                if (Chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Contract download is only supported for single-node consensus");
                }

                throw new NotImplementedException();
                // return NodeUtility.PersistContract(neoSystem, state, storagePairs, force);
            });

        // warning CS1998: This async method lacks 'await' operators and will run synchronously.
        // EnumerateNotificationsAsync has to be async in order to be polymorphic with OnlineNode's implementation
#pragma warning disable 1998 
        public async IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter)
        {
            var notifications = expressSystem.GetNotifications(SeekDirection.Backward, contractFilter, eventFilter);
            foreach (var (block, _, notification) in notifications)
            {
                yield return (block, notification);
            }
        }
#pragma warning restore 1998
    }
}
