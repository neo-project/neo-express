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
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.Wallets;
using NeoExpress.Models;

using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    internal class OfflineNode : IDisposable, IExpressNode
    {
        private readonly NeoSystem neoSystem;
        private readonly ExpressApplicationEngineProvider? applicationEngineProvider;
        private readonly Wallet nodeWallet;
        private readonly ExpressChain chain;
        private readonly RocksDbStorageProvider rocksDbStorageProvider;
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
            
            var storageProviderPlugin = new StorageProviderPlugin(rocksDbStorageProvider);
            _ = new ExpressAppLogsPlugin(rocksDbStorageProvider);
            neoSystem = new NeoSystem(settings, storageProviderPlugin.Name);

            ApplicationEngine.Log += OnLog!;
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

// Disable "This async method lacks 'await' operators and will run synchronously" warnings.
// these methods are async because of the OnlineNode implementation of IExpressNode
// using async methods ensures exceptions in these methods are returned to calling code correctly
#pragma warning disable 1998

        public async Task<RpcInvokeResult> InvokeAsync(Neo.VM.Script script)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using ApplicationEngine engine = script.Invoke(neoSystem.Settings, neoSystem.StoreView);
            var result = new RpcInvokeResult()
            {
                State = engine.State,
                Exception = engine.FaultException?.GetBaseException().Message ?? string.Empty,
                GasConsumed = engine.GasConsumed,
                Stack = engine.ResultStack.ToArray(),
                Script = string.Empty,
                Tx = string.Empty
            };
            return result;
        }

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, Neo.VM.Script script, decimal additionalGas = 0)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var signer = new Signer() { Account = accountHash, Scopes = WitnessScope.CalledByEntry };
            var tx = wallet.MakeTransaction(neoSystem.StoreView, script, accountHash, new[] { signer });
            if (additionalGas > 0.0m)
            {
                tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }

            var context = new ContractParametersContext(neoSystem.StoreView, tx);
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
            return await SubmitTransactionAsync(tx);
        }

        public async Task<UInt256> SubmitTransactionAsync(Transaction tx)
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

            // First, we make verify the provided transactions. When running, Neo does verification in two steps: VerifyStateIndependent
            // and VerifyStateDependent. However, Verify does both parts and there's no point in verifying offline in separate steps.
            var verificationContext = new TransactionVerificationContext();
            for (int i = 0; i < transactions.Length; i++)
            {
                var q = transactions[i].Size * NativeContract.Policy.GetFeePerByte(snapshot);
                if (transactions[i].Verify(ProtocolSettings, snapshot, verificationContext) != VerifyResult.Succeed)
                {
                    throw new Exception("Verification failed");
                }
            }

            // Then we create the block instance
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

            // finally we sign the block, following the logic in ConsensusContext.MakeCommit (create signature)
            // and ConsensusContext.CreateBlock (sign block)
            var (_, genesisAccount) = chain.GetGenesisAccount(ProtocolSettings);

            var signingContext = new ContractParametersContext(snapshot, block.Header);
            foreach (var node in chain.ConsensusNodes)
            {
                var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
                var multiSigAccount = nodeWallet.GetMultiSigAccounts().Single();
                var key = multiSigAccount.GetKey() ?? throw new Exception();

                var signature = block.Header.Sign(key, ProtocolSettings.Magic);
                signingContext.AddSignature(genesisAccount.Contract, key.PublicKey, signature);
                if (signingContext.Completed) break;
            }
            if (!signingContext.Completed) throw new Exception("block signing incomplete");
            block.Header.Witness = signingContext.GetWitnesses()[0];

            return block;
        }

        protected virtual void Dispose(bool disposing)
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

        public async Task<(RpcNep17Balance balance, Nep17Contract contract)[]> GetBalancesAsync(UInt160 address)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contracts = ExpressRpcServer.GetNep17Contracts(neoSystem, rocksDbStorageProvider).ToDictionary(c => c.ScriptHash);
            var balances = ExpressRpcServer.GetNep17Balances(neoSystem, rocksDbStorageProvider, address)
                .Select(b => (
                    balance: new RpcNep17Balance
                    {
                        Amount = b.balance,
                        AssetHash = b.contract.ScriptHash,
                        LastUpdatedBlock = b.lastUpdatedBlock
                    },
                    contract: contracts.TryGetValue(b.contract.ScriptHash, out var value)
                        ? value
                        : Nep17Contract.Unknown(b.contract.ScriptHash)))
                .ToArray();

            return balances;
        }

        public async Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var tx = NativeContract.Ledger.GetTransaction(neoSystem.StoreView, txHash);
            var log = ExpressAppLogsPlugin.GetAppLog(rocksDbStorageProvider, txHash);
            return (tx, log != null ? RpcApplicationLog.FromJson(log, ProtocolSettings) : null);
        }

        public async Task<Block> GetBlockAsync(UInt256 blockHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));
            var block = NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockHash);
            return block;
        }

        public async Task<Block> GetBlockAsync(uint blockIndex)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));
            var block = NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockIndex);
            return block;
        }

        public async Task<Block> GetLatestBlockAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            var block = NativeContract.Ledger.GetBlock(snapshot, hash);
            return block;
        }

        public async Task<uint> GetTransactionHeightAsync(UInt256 txHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            uint? height = NativeContract.Ledger.GetTransactionState(neoSystem.StoreView, txHash)?.BlockIndex;
            return height.HasValue ? height.Value : throw new Exception("Unknown transaction");
        }

        public async Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(UInt160 scriptHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);

            if (contract != null)
            {
                byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
                return snapshot.Find(prefix)
                    .Select(t => new ExpressStorage()
                    {
                        Key = t.Key.Key.ToHexString(),
                        Value = t.Value.Value.ToHexString(),
                    })
                    .ToList();
            }

            return Array.Empty<ExpressStorage>();
        }

        public async Task<ContractManifest> GetContractAsync(UInt160 scriptHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contractState = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contractState == null)
            {
                throw new Exception("Unknown contract");
            }
            return contractState.Manifest;
        }

        public async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            return NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
                .OrderBy(c => c.Id)
                .Select(c => (c.Hash, c.Manifest))
                .ToList();
        }

        public async Task<IReadOnlyList<Nep17Contract>> ListNep17ContractsAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            return ExpressRpcServer.GetNep17Contracts(neoSystem, rocksDbStorageProvider).ToList();
        }

        public async Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var requests = NativeContract.Oracle.GetRequests(neoSystem.StoreView).ToList();
            return requests;
        }

        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, ECPoint[] oracleNodes)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();

            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = OracleService.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings);
            if (tx == null) throw new Exception("Failed to create Oracle Response Tx");
            ExpressOracle.SignOracleResponseTransaction(ProtocolSettings, chain, tx, oracleNodes);
            return await SubmitTransactionAsync(tx);
        }

        public async Task<bool> CreateCheckpointAsync(string checkPointPath)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var multiSigAccount = nodeWallet.GetMultiSigAccounts().Single();
            rocksDbStorageProvider.CreateCheckpoint(checkPointPath, ProtocolSettings, multiSigAccount.ScriptHash);
            return false;
        }
#pragma warning restore // "This async method lacks 'await' operators and will run synchronously" 
    }
}
