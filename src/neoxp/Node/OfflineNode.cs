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
using Neo.Persistence;
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
        private readonly ExpressStorageProvider storageProvider;
        private readonly ExpressApplicationEngineProvider? applicationEngineProvider;
        private readonly Wallet nodeWallet;
        private readonly ExpressChain chain;
        private readonly IExpressStore store;
        private bool disposedValue;

        public ProtocolSettings ProtocolSettings { get; }

        public OfflineNode(ProtocolSettings settings, IExpressStore store, ExpressWallet nodeWallet, ExpressChain chain, bool enableTrace)
            : this(settings, store, DevWallet.FromExpressWallet(settings, nodeWallet), chain, enableTrace)
        {
        }

        public OfflineNode(ProtocolSettings settings, IExpressStore store, Wallet nodeWallet, ExpressChain chain, bool enableTrace)
        {
            this.ProtocolSettings = settings;
            this.nodeWallet = nodeWallet;
            this.chain = chain;
            this.store = store;
            applicationEngineProvider = enableTrace ? new ExpressApplicationEngineProvider() : null;
            storageProvider = new ExpressStorageProvider((IStore)store);
            _ = new ExpressAppLogsPlugin(store);
            neoSystem = new NeoSystem(settings, storageProvider.Name);

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

        public Task<RpcInvokeResult> InvokeAsync(Neo.VM.Script script)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using ApplicationEngine engine = ApplicationEngine.Run(
                script: script,
                snapshot: neoSystem.StoreView, 
                settings: ProtocolSettings);

            var result = new RpcInvokeResult()
            {
                State = engine.State,
                Exception = engine.FaultException?.GetBaseException().Message ?? string.Empty,
                GasConsumed = engine.GasConsumed,
                Stack = engine.ResultStack.ToArray(),
                Script = string.Empty,
                Tx = string.Empty
            };
            return Task.FromResult(result);
        }

        public Task<UInt256> ExecuteAsync(ExpressWalletAccount account, Neo.VM.Script script, decimal additionalGas = 0)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var devAccount = DevWalletAccount.FromExpressWalletAccount(ProtocolSettings, account);
            var devWallet = new DevWallet(ProtocolSettings, string.Empty, devAccount);
            var signer = new Signer() { Account = devAccount.ScriptHash, Scopes = WitnessScope.CalledByEntry };
            var tx = devWallet.MakeTransaction(neoSystem.StoreView, script, devAccount.ScriptHash, new[] { signer });
            if (additionalGas > 0.0m)
            {
                tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }
            var context = new ContractParametersContext(neoSystem.StoreView, tx);

            if (devAccount.IsMultiSigContract())
            {
                var wallets = chain.GetMultiSigWallets(account);

                foreach (var wallet in wallets)
                {
                    if (context.Completed) break;

                    wallet.Sign(context);
                }
            }
            else
            {
                devWallet.Sign(context);
            }

            if (!context.Completed)
            {
                throw new Exception();
            }

            tx.Witnesses = context.GetWitnesses();

            return SubmitTransactionAsync(tx);
        }

        public async Task<UInt256> SubmitTransactionAsync(Transaction tx)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var block = CreateSignedBlock(new [] { tx });
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
            var genesis = DevWalletAccount.FromExpressWalletAccount(ProtocolSettings, chain.GetGenesisAccount());
            var signingContext = new ContractParametersContext(snapshot, block.Header);
            foreach (var node in chain.ConsensusNodes)
            {
                var account = DevWalletAccount.FromExpressWalletAccount(ProtocolSettings, node.Wallet.Accounts.Single(a => a.IsMultiSigContract()));
                var key = account.GetKey() ?? throw new Exception();

                var signature = block.Header.Sign(key, ProtocolSettings.Magic);
                signingContext.AddSignature(genesis.Contract, key.PublicKey, signature);
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
                    storageProvider.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task<(RpcNep17Balance balance, Nep17Contract contract)[]> GetBalancesAsync(UInt160 address)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contracts = ExpressRpcServer.GetNep17Contracts(neoSystem, store).ToDictionary(c => c.ScriptHash);
            var balances = ExpressRpcServer.GetNep17Balances(neoSystem, store, address)
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
            return Task.FromResult(balances);
        }

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var tx = NativeContract.Ledger.GetTransaction(neoSystem.StoreView, txHash);
            var log = ExpressAppLogsPlugin.TryGetAppLog(store, txHash);
            return Task.FromResult((tx, log != null ? RpcApplicationLog.FromJson(log, ProtocolSettings) : null));
        }

        public Task<Block> GetBlockAsync(UInt256 blockHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));
            var block = NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockHash);
            return Task.FromResult(block);
        }

        public Task<Block> GetBlockAsync(uint blockIndex)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));
            var block = NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockIndex);
            return Task.FromResult(block);
        }

        public Task<Block> GetLatestBlockAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            var block = NativeContract.Ledger.GetBlock(snapshot, hash);
            return Task.FromResult(block);
        }

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            uint? height = NativeContract.Ledger.GetTransactionState(neoSystem.StoreView, txHash)?.BlockIndex;
            return height.HasValue
                ? Task.FromResult(height.Value)
                : Task.FromException<uint>(new Exception("Unknown transaction"));
        }

        public Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(UInt160 scriptHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();            
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);

            if (contract != null)
            {
                byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
                IReadOnlyList<ExpressStorage> storages = snapshot.Find(prefix)
                    .Select(t => new ExpressStorage()
                    {
                        Key = t.Key.Key.ToHexString(),
                        Value = t.Value.Value.ToHexString(),
                    })
                    .ToList();
                return Task.FromResult(storages);
            }

            return Task.FromResult<IReadOnlyList<ExpressStorage>>(Array.Empty<ExpressStorage>());
        }

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contractState = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contractState == null)
            {
                throw new Exception("Unknown contract");
            }
            return Task.FromResult(contractState.Manifest);
        }

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contracts = NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
                .OrderBy(c => c.Id)
                .Select(c => (c.Hash, c.Manifest))
                .ToList();

            return Task.FromResult<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>>(contracts);
        }

        public Task<IReadOnlyList<Nep17Contract>> ListNep17ContractsAsync()
        {
            return Task.FromResult<IReadOnlyList<Nep17Contract>>(
                ExpressRpcServer.GetNep17Contracts(neoSystem, store).ToList());
        }

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            var requests = NativeContract.Oracle.GetRequests(neoSystem.StoreView).ToList();
            return Task.FromResult<IReadOnlyList<(ulong, OracleRequest)>>(requests);
        }

        public Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, ECPoint[] oracleNodes)
        {
            using var snapshot = neoSystem.GetSnapshot();

            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = OracleService.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings);
            if (tx == null) throw new Exception("Failed to create Oracle Response Tx");
            ExpressOracle.SignOracleResponseTransaction(ProtocolSettings, chain, tx, oracleNodes);
            return SubmitTransactionAsync(tx);
        }

        public Task<bool> CreateCheckpointAsync(string checkPointPath)
        {
            if (store is RocksDbStore rocksDbStore)
            {
                var multiSigAccount = nodeWallet.GetAccounts().Single(a => a.IsMultiSigContract());
                rocksDbStore.CreateCheckpoint(checkPointPath, ProtocolSettings, multiSigAccount.ScriptHash.ToAddress(ProtocolSettings.AddressVersion));
                return Task.FromResult(false);
            }

            return Task.FromException<bool>(new Exception());
        }
    }
}
