using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Akka.Actor;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    internal sealed class OfflineNode : IDisposable, IExpressNode
    {
        private readonly NeoSystem neoSystem;
        private readonly ApplicationEngineProvider? applicationEngineProvider;
        private readonly Wallet nodeWallet;
        private readonly ExpressChain chain;
        private readonly RocksDbStorageProvider rocksDbStorageProvider;
        private readonly Lazy<KeyPair[]> consensusNodesKeys;
        private bool disposedValue;

        public ProtocolSettings ProtocolSettings => neoSystem.Settings;

        public OfflineNode(ProtocolSettings settings, RocksDbStorageProvider rocksDbStorageProvider, ExpressWallet nodeWallet, ExpressChain chain, bool enableTrace)
            : this(settings, rocksDbStorageProvider, DevWallet.FromExpressWallet(settings, nodeWallet), chain, enableTrace)
        {
        }

        public OfflineNode(ProtocolSettings settings, RocksDbStorageProvider rocksDbStorageProvider, Wallet nodeWallet, ExpressChain chain, bool enableTrace)
        {
            this.nodeWallet = nodeWallet;
            this.chain = chain;
            this.rocksDbStorageProvider = rocksDbStorageProvider;
            applicationEngineProvider = enableTrace ? new ApplicationEngineProvider() : null;
            consensusNodesKeys = new Lazy<KeyPair[]>(() => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception())
                .Select(a => new KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray());

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
                var multiSigWallets = chain.GetMultiSigWallets(neoSystem.Settings, accountHash);
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
            NodeUtility.SignOracleResponseTransaction(ProtocolSettings, chain, tx, oracleNodes);

            var blockHash = await SubmitTransactionAsync(tx);
            return tx.Hash;
        }

        public async Task FastForwardAsync(uint blockCount)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            for (int i = 0; i < blockCount; i++)
            {
                await SubmitTransactionAsync(null).ConfigureAwait(false);
            }
        }

        async Task<UInt256> SubmitTransactionAsync(Transaction? tx = null)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var transactions = tx == null ? Array.Empty<Transaction>() : new[] { tx };
            var block = CreateSignedBlock(neoSystem, consensusNodesKeys.Value, transactions);
            var blockRelay = await neoSystem.Blockchain.Ask<RelayResult>(block).ConfigureAwait(false);
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }
            return block.Hash;

            static Block CreateSignedBlock(NeoSystem neoSystem, KeyPair[] consensusNodesKeys, Transaction[] transactions)
            {
                // The logic in this method is distilled from ConsensusService/ConsensusContext + MemPool tx verification logic

                var snapshot = neoSystem.StoreView;

                // Verify the provided transactions. When running, Blockchain class does verification in two steps: VerifyStateIndependent and VerifyStateDependent.
                // However, Verify does both parts and there's no point in verifying dependent/independent in separate steps here
                var verificationContext = new TransactionVerificationContext();
                for (int i = 0; i < transactions.Length; i++)
                {
                    if (transactions[i].Verify(neoSystem.Settings, snapshot, verificationContext) != VerifyResult.Succeed)
                    {
                        throw new Exception("Verification failed");
                    }
                }

                // create the block instance
                var prevHash = NativeContract.Ledger.CurrentHash(snapshot);
                var prevBlock = NativeContract.Ledger.GetHeader(snapshot, prevHash);
                var blockHeight = prevBlock.Index + 1;
                var block = new Block
                {
                    Header = new Header
                    {
                        Version = 0,
                        PrevHash = prevHash,
                        MerkleRoot = MerkleTree.ComputeRoot(transactions.Select(t => t.Hash).ToArray()),
                        Timestamp = Math.Max(Neo.Helper.ToTimestampMS(DateTime.UtcNow), prevBlock.Timestamp + 1),
                        Index = blockHeight,
                        PrimaryIndex = 0,
                        NextConsensus = Contract.GetBFTAddress(
                            NeoToken.ShouldRefreshCommittee(blockHeight, neoSystem.Settings.CommitteeMembersCount)
                                ? NativeContract.NEO.ComputeNextBlockValidators(snapshot, neoSystem.Settings)
                                : NativeContract.NEO.GetNextBlockValidators(snapshot, neoSystem.Settings.ValidatorsCount)),
                    },
                    Transactions = transactions
                };

                // retrieve the validators for the next block. Logic lifted from ConsensusContext.Reset
                var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, neoSystem.Settings.ValidatorsCount);
                var m = validators.Length - (validators.Length - 1) / 3;

                // generate the block header witness. Logic lifted from ConsensusContext.CreateBlock
                var contract = Contract.CreateMultiSigContract(m, validators);
                var signingContext = new ContractParametersContext(snapshot, block.Header, neoSystem.Settings.Network);
                for (int i = 0, j = 0; i < validators.Length && j < m; i++)
                {
                    var key = consensusNodesKeys.SingleOrDefault(k => k.PublicKey.Equals(validators[i]));
                    if (key == null) continue;

                    var signature = block.Header.Sign(key, neoSystem.Settings.Network);
                    signingContext.AddSignature(contract, validators[i], signature);
                    j++;
                }
                if (!signingContext.Completed) throw new Exception("block signing incomplete");
                block.Header.Witness = signingContext.GetWitnesses()[0];

                return block;
            }
        }

        public Task<Block> GetBlockAsync(UInt256 blockHash)
            => MakeAsync(() => NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockHash));

        public Task<Block> GetBlockAsync(uint blockIndex)
            => MakeAsync(() => NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockIndex));

        ContractManifest GetContract(UInt160 scriptHash)
        {
            var contractState = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contractState == null) throw new Exception("Unknown contract");
            return contractState.Manifest;
        }

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash)
            => MakeAsync(() => GetContract(scriptHash));

        Block GetLatestBlock()
        {
            using var snapshot = neoSystem.GetSnapshot();
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            return NativeContract.Ledger.GetBlock(snapshot, hash);
        }

        public Task<Block> GetLatestBlockAsync() => MakeAsync(GetLatestBlock);

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

        uint GetTransactionHeight(UInt256 txHash)
        {
            var height = NativeContract.Ledger.GetTransactionState(neoSystem.StoreView, txHash)?.BlockIndex;
            return height.HasValue
                ? height.Value
                : throw new Exception("Unknown Transaction");
        }

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash)
            => MakeAsync(() => GetTransactionHeight(txHash));

        IReadOnlyList<(TokenContract contract, BigInteger balance)> ListBalances(UInt160 address)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var contracts = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == TokenStandard.Nep17);

            var addressArray = address.ToArray();
            var contractCount = 0;
            using var builder = new ScriptBuilder();
            foreach (var c in contracts.Reverse())
            {
                builder.EmitDynamicCall(c.scriptHash, "symbol");
                builder.EmitDynamicCall(c.scriptHash, "decimals");
                builder.EmitDynamicCall(c.scriptHash, "balanceOf", addressArray);
                contractCount++;
            }

            List<(TokenContract contract, BigInteger balance)> balances = new();
            using var engine = builder.Invoke(neoSystem.Settings, snapshot);
            if (engine.State != VMState.FAULT && engine.ResultStack.Count == contractCount * 3)
            {
                var resultStack = engine.ResultStack;
                for (var i = 0; i < contractCount; i++)
                {
                    var index = i * 3;
                    var symbol = resultStack.Peek(index + 2).GetString();
                    if (symbol == null) continue;
                    var decimals = (byte)resultStack.Peek(index + 1).GetInteger();
                    var balance = resultStack.Peek(index).GetInteger();
                    var (scriptHash, standard) = contracts.ElementAt(i);
                    balances.Add((new TokenContract(symbol, decimals, scriptHash, standard), balance));
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

        IReadOnlyList<ExpressStorage> ListStorages(UInt160 scriptHash)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);

            if (contract == null) return Array.Empty<ExpressStorage>();

            byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
            return snapshot.Find(prefix)
                .Select(t => new ExpressStorage()
                {
                    Key = t.Key.Key.ToHexString(),
                    Value = t.Value.Value.ToHexString(),
                })
                .ToList();
        }

        public Task<IReadOnlyList<ExpressStorage>> ListStoragesAsync(UInt160 scriptHash)
            => MakeAsync(() => ListStorages(scriptHash));
        
        int PersistContract(ContractState state, (string key, string value)[] storagePairs)
        {
            return NodeUtility.PersistContract(neoSystem.GetSnapshot(), state, storagePairs);
        }

        public Task<int> PersistContractAsync(ContractState state, (string key, string value)[] storagePairs) 
            => MakeAsync(() => PersistContract(state, storagePairs));

    }
}
