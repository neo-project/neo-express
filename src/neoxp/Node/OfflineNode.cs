using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.Wallets;
using NeoExpress.Models;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    internal sealed class OfflineNode : IDisposable, IExpressNode
    {
        private readonly NeoSystem neoSystem;
        private readonly ExpressApplicationEngineProvider? applicationEngineProvider;
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
            applicationEngineProvider = enableTrace ? new ExpressApplicationEngineProvider() : null;
            consensusNodesKeys = new Lazy<KeyPair[]>(() => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception())
                .Select(a => new KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray());

            var storageProviderPlugin = new StorageProviderPlugin(rocksDbStorageProvider);
            _ = new ExpressAppLogsPlugin(rocksDbStorageProvider);
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

        public Task<IExpressNode.CheckpointMode> CreateCheckpointAsync(string checkPointPath)
        {
            try
            {
                if (disposedValue) return Task.FromException<IExpressNode.CheckpointMode>(new ObjectDisposedException(nameof(OfflineNode)));

                var multiSigAccount = nodeWallet.GetMultiSigAccounts().Single();
                rocksDbStorageProvider.CreateCheckpoint(checkPointPath, ProtocolSettings, multiSigAccount.ScriptHash);
                return Task.FromResult(IExpressNode.CheckpointMode.Offline);
            }
            catch (Exception ex)
            {
                return Task.FromException<IExpressNode.CheckpointMode>(ex);
            }
        }

        public Task<RpcInvokeResult> InvokeAsync(Neo.VM.Script script, Signer? signer = null)
        {
            try
            {
                if (disposedValue) return Task.FromException<RpcInvokeResult>(new ObjectDisposedException(nameof(OfflineNode)));

                Transaction? tx = signer != null
                    ? new Transaction
                        {
                            Signers = new[] { signer },
                            Attributes = Array.Empty<TransactionAttribute>(),
                            Witnesses = Array.Empty<Witness>(),
                        }
                    : null;

                using ApplicationEngine engine = script.Invoke(neoSystem.Settings, neoSystem.StoreView, tx);
                return Task.FromResult(new RpcInvokeResult()
                {
                    State = engine.State,
                    Exception = engine.FaultException?.GetBaseException().Message ?? string.Empty,
                    GasConsumed = engine.GasConsumed,
                    Stack = engine.ResultStack.ToArray(),
                    Script = string.Empty,
                    Tx = string.Empty
                });
            }
            catch (Exception ex)
            {
                return Task.FromException<RpcInvokeResult>(ex);
            }
        }

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Neo.VM.Script script, decimal additionalGas = 0)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var signer = new Signer() { Account = accountHash, Scopes = witnessScope };
            var tx = wallet.MakeTransaction(neoSystem.StoreView, script, accountHash, new[] { signer });
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
            return await SubmitTransactionAsync(tx).ConfigureAwait(false);
        }

        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();
            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = ExpressOracle.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings);
            if (tx == null) throw new Exception("Failed to create Oracle Response Tx");
            ExpressOracle.SignOracleResponseTransaction(ProtocolSettings, chain, tx, oracleNodes);

            return await SubmitTransactionAsync(tx);
        }

        async Task<UInt256> SubmitTransactionAsync(Transaction tx)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var block = CreateSignedBlock(new[] { tx });
            var blockRelay = await neoSystem.Blockchain.Ask<RelayResult>(block);
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }

            return tx.Hash;
        }

        Block CreateSignedBlock(Transaction[] transactions)
        {
            // The logic in this method is distilled from ConsensusService/ConsensusContext + MemPool tx verification logic

            var snapshot = neoSystem.StoreView;

            // Verify the provided transactions. When running, Blockchain class does verification in two steps: VerifyStateIndependent and VerifyStateDependent.
            // However, Verify does both parts and there's no point in verifying dependent/independent in separate steps here
            var verificationContext = new TransactionVerificationContext();
            for (int i = 0; i < transactions.Length; i++)
            {
                var q = transactions[i].Size * NativeContract.Policy.GetFeePerByte(snapshot);
                if (transactions[i].Verify(ProtocolSettings, snapshot, verificationContext) != VerifyResult.Succeed)
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
                        NeoToken.ShouldRefreshCommittee(blockHeight, ProtocolSettings.CommitteeMembersCount)
                            ? NativeContract.NEO.ComputeNextBlockValidators(snapshot, ProtocolSettings)
                            : NativeContract.NEO.GetNextBlockValidators(snapshot, ProtocolSettings.ValidatorsCount)),
                },
                Transactions = transactions
            };

            // retrieve the validators for the next block. Logic lifted from ConensusContext.Reset
            var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, ProtocolSettings.ValidatorsCount);
            var m = validators.Length - (validators.Length - 1) / 3;

            // generate the block header witness. Logic lifted from ConsensusContext.CreateBlock
            var contract = Contract.CreateMultiSigContract(m, validators);
            var signingContext = new ContractParametersContext(snapshot, block.Header, ProtocolSettings.Network);
            for (int i = 0, j = 0; i < validators.Length && j < m; i++)
            {
                var key = consensusNodesKeys.Value.SingleOrDefault(k => k.PublicKey.Equals(validators[i]));
                if (key == null) continue;

                var signature = block.Header.Sign(key, ProtocolSettings.Network);
                signingContext.AddSignature(contract, validators[i], signature);
                j++;
            }
            if (!signingContext.Completed) throw new Exception("block signing incomplete");
            block.Header.Witness = signingContext.GetWitnesses()[0];

            return block;
        }

        public Task<Block> GetBlockAsync(UInt256 blockHash)
        {
            try
            {
                if (disposedValue) return Task.FromException<Block>(new ObjectDisposedException(nameof(OfflineNode)));

                return Task.FromResult(NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockHash));
            }
            catch (Exception ex)
            {
                return Task.FromException<Block>(ex);
            }
        }

        public Task<Block> GetBlockAsync(uint blockIndex)
        {
            try
            {
                if (disposedValue) return Task.FromException<Block>(new ObjectDisposedException(nameof(OfflineNode)));

                return Task.FromResult(NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockIndex));
            }
            catch (Exception ex)
            {
                return Task.FromException<Block>(ex);
            }
        }

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash)
        {
            try
            {
                if (disposedValue) return Task.FromException<ContractManifest>(new ObjectDisposedException(nameof(OfflineNode)));

                var contractState = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
                return contractState != null
                    ? Task.FromResult(contractState.Manifest)
                    : Task.FromException<ContractManifest>(new Exception("Unknown contract"));
            }
            catch (Exception ex)
            {
                return Task.FromException<ContractManifest>(ex);
            }
        }

        public Task<Block> GetLatestBlockAsync()
        {
            try
            {
                if (disposedValue) return Task.FromException<Block>(new ObjectDisposedException(nameof(OfflineNode)));

                using var snapshot = neoSystem.GetSnapshot();
                var hash = NativeContract.Ledger.CurrentHash(snapshot);
                return Task.FromResult(NativeContract.Ledger.GetBlock(snapshot, hash));
            }
            catch (Exception ex)
            {
                return Task.FromException<Block>(ex);
            }
        }

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
        {
            try
            {
                if (disposedValue) return Task.FromException<(Transaction, RpcApplicationLog?)>(new ObjectDisposedException(nameof(OfflineNode)));

                var tx = NativeContract.Ledger.GetTransaction(neoSystem.StoreView, txHash);
                if (tx == null) return Task.FromException<(Transaction, RpcApplicationLog?)>(new Exception("Unknown Transaction"));

                var jsonLog = ExpressAppLogsPlugin.GetAppLog(rocksDbStorageProvider, txHash);
                var result = jsonLog != null
                    ? (tx, RpcApplicationLog.FromJson(jsonLog, ProtocolSettings))
                    : (tx, null);

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromException<(Transaction, RpcApplicationLog?)>(ex);
            }
        }

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash)
        {
            try
            {
                if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

                var height = NativeContract.Ledger.GetTransactionState(neoSystem.StoreView, txHash)?.BlockIndex;
                return height.HasValue
                    ? Task.FromResult(height.Value)
                    : Task.FromException<uint>(new Exception("Unknown Transaction"));
            }
            catch (Exception ex)
            {
                return Task.FromException<uint>(ex);
            }
        }

        public Task<IReadOnlyList<(RpcNep17Balance balance, Nep17Contract contract)>> ListBalancesAsync(UInt160 address)
        {
            try
            {
                if (disposedValue) return Task.FromException<IReadOnlyList<(RpcNep17Balance, Nep17Contract)>>(new ObjectDisposedException(nameof(OfflineNode)));

                var contractMap = ExpressRpcServer.GetNep17Contracts(neoSystem, rocksDbStorageProvider).ToDictionary(c => c.ScriptHash);
                var results = ExpressRpcServer.GetNep17Balances(neoSystem, rocksDbStorageProvider, address)
                    .Select(b => (
                        balance: new RpcNep17Balance
                        {
                            Amount = b.balance,
                            AssetHash = b.contract.ScriptHash,
                            LastUpdatedBlock = b.lastUpdatedBlock
                        },
                        contract: contractMap.TryGetValue(b.contract.ScriptHash, out var value)
                            ? value
                            : Nep17Contract.Unknown(b.contract.ScriptHash)));

                return Task.FromResult<IReadOnlyList<(RpcNep17Balance, Nep17Contract)>>(results.ToArray());
            }
            catch (Exception ex)
            {
                return Task.FromException<IReadOnlyList<(RpcNep17Balance, Nep17Contract)>>(ex);
            }
        }

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
        {
            try
            {
                if (disposedValue) return Task.FromException<IReadOnlyList<(UInt160, ContractManifest)>>(new ObjectDisposedException(nameof(OfflineNode)));

                var contracts = NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
                    .OrderBy(c => c.Id)
                    .Select(c => (c.Hash, c.Manifest));

                return Task.FromResult<IReadOnlyList<(UInt160, ContractManifest)>>(contracts.ToArray());
            }
            catch (Exception ex)
            {
                return Task.FromException<IReadOnlyList<(UInt160, ContractManifest)>>(ex);
            }
        }

        public Task<IReadOnlyList<Nep17Contract>> ListNep17ContractsAsync()
        {
            try
            {
                if (disposedValue) return Task.FromException<IReadOnlyList<Nep17Contract>>(new ObjectDisposedException(nameof(OfflineNode)));

                var contracts = ExpressRpcServer.GetNep17Contracts(neoSystem, rocksDbStorageProvider);

                return Task.FromResult<IReadOnlyList<Nep17Contract>>(contracts.ToArray());
            }
            catch (Exception ex)
            {
                return Task.FromException<IReadOnlyList<Nep17Contract>>(ex);
            }
        }

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            try
            {
                if (disposedValue) return Task.FromException<IReadOnlyList<(ulong, OracleRequest)>>(new ObjectDisposedException(nameof(OfflineNode)));

                var requests = NativeContract.Oracle.GetRequests(neoSystem.StoreView);

                return Task.FromResult<IReadOnlyList<(ulong, OracleRequest)>>(requests.ToArray());
            }
            catch (Exception ex)
            {
                return Task.FromException<IReadOnlyList<(ulong, OracleRequest)>>(ex);
            }
        }

        public Task<IReadOnlyList<ExpressStorage>> ListStoragesAsync(UInt160 scriptHash)
        {
            try
            {
                if (disposedValue) return Task.FromException<IReadOnlyList<ExpressStorage>>(new ObjectDisposedException(nameof(OfflineNode)));

                using var snapshot = neoSystem.GetSnapshot();
                var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);

                if (contract == null) return Task.FromResult<IReadOnlyList<ExpressStorage>>(Array.Empty<ExpressStorage>());

                byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
                var results = snapshot.Find(prefix)
                    .Select(t => new ExpressStorage()
                    {
                        Key = t.Key.Key.ToHexString(),
                        Value = t.Value.Value.ToHexString(),
                    });

                return Task.FromResult<IReadOnlyList<ExpressStorage>>(results.ToArray());
            }
            catch (Exception ex)
            {
                return Task.FromException<IReadOnlyList<ExpressStorage>>(ex);
            }
        }
    }
}
