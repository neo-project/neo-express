using Akka.Actor;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Consensus;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal class OfflineNode : IDisposable, IExpressNode
    {
        private readonly NeoSystem neoSystem;
        private readonly ExpressStorageProvider storageProvider;
        private readonly ExpressApplicationEngineProvider? applicationEngineProvider;
        private readonly Wallet nodeWallet;
        private readonly ExpressChain chain;
        private readonly IExpressStore store;
        private bool disposedValue;

        public OfflineNode(IExpressStore store, ExpressWallet nodeWallet, ExpressChain chain, bool enableTrace)
            : this(store, DevWallet.FromExpressWallet(nodeWallet), chain, enableTrace)
        {
        }

        public OfflineNode(IExpressStore store, Wallet nodeWallet, ExpressChain chain, bool enableTrace)
        {
            this.nodeWallet = nodeWallet;
            this.chain = chain;
            this.store = store;
            applicationEngineProvider = enableTrace ? new ExpressApplicationEngineProvider() : null;
            storageProvider = new ExpressStorageProvider((IStore)store);
            _ = new ExpressAppLogsPlugin(store);
            neoSystem = new NeoSystem(storageProvider.Name);

            ApplicationEngine.Log += OnLog!;
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            var engine = sender as ApplicationEngine;
            var tx = engine?.ScriptContainer as Transaction;
            var colorCode = tx?.Witnesses?.Any() ?? false ? "96" : "93";
            var contract = NativeContract.ContractManagement.GetContract(Blockchain.Singleton.View, args.ScriptHash);
            var name = contract == null ? args.ScriptHash.ToString() : contract.Manifest.Name;
            Console.WriteLine($"\x1b[35m{name}\x1b[0m Log: \x1b[{colorCode}m\"{args.Message}\"\x1b[0m [{args.ScriptContainer.GetType().Name}]");
        }

        public Task<RpcInvokeResult> InvokeAsync(Neo.VM.Script script)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using ApplicationEngine engine = ApplicationEngine.Run(script, container: null, gas: 20000000L);
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

            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);
            var devWallet = new DevWallet(string.Empty, devAccount);
            var signer = new Signer() { Account = devAccount.ScriptHash, Scopes = WitnessScope.CalledByEntry };
            var tx = devWallet.MakeTransaction(script, devAccount.ScriptHash, new[] { signer });
            if (additionalGas > 0.0m)
            {
                tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }
            var context = new ContractParametersContext(tx);

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

            var txRelay = await neoSystem.Blockchain.Ask<RelayResult>(tx);
            if (txRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Transaction relay failed {txRelay.Result}");
            }

            var block = RunConsensus();
            var blockRelay = await neoSystem.Blockchain.Ask<RelayResult>(block);
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }

            return tx.Hash;
        }

        Block RunConsensus()
        {
            if (chain.ConsensusNodes.Count == 1)
            {
                var ctx = new ConsensusContext(nodeWallet, Blockchain.Singleton.Store);
                ctx.Reset(0);
                ctx.MakePrepareRequest();
                ctx.MakeCommit();
                return ctx.CreateBlock();
            }

            // create ConsensusContext for each ConsensusNode
            var contexts = new ConsensusContext[chain.ConsensusNodes.Count];
            for (int x = 0; x < contexts.Length; x++)
            {
                contexts[x] = new ConsensusContext(DevWallet.FromExpressWallet(chain.ConsensusNodes[x].Wallet), Blockchain.Singleton.Store);
                contexts[x].Reset(0);
            }

            // find the primary node for this consensus round
            var primary = contexts.Single(c => c.IsPrimary);
            var prepareRequestPayload = primary.MakePrepareRequest();

            for (int x = 0; x < contexts.Length; x++)
            {
                var context = contexts[x];
                if (context.MyIndex == primary.MyIndex) continue;
                var prepareRequestMessage = context.GetMessage<PrepareRequest>(prepareRequestPayload);
                OnPrepareRequestReceived(context, prepareRequestPayload, prepareRequestMessage);
                var commitPayload = context.MakeCommit();
                var commitMessage = primary.GetMessage<Commit>(commitPayload);
                OnCommitReceived(primary, commitPayload, commitMessage);
            }

            return primary.CreateBlock();
        }

        // TODO: remove if https://github.com/neo-project/neo/issues/2061 is fixed
        // this logic is lifted from ConsensusService.OnPrepareRequestReceived
        // Log, Timer, Task and CheckPrepare logic has been commented out for offline consensus
        private static void OnPrepareRequestReceived(ConsensusContext context, ExtensiblePayload payload, PrepareRequest message)
        {
            if (context.RequestSentOrReceived || context.NotAcceptingPayloadsDueToViewChanging) return;
            if (message.ValidatorIndex != context.Block.ConsensusData.PrimaryIndex || message.ViewNumber != context.ViewNumber) return;
            if (message.Version != context.Block.Version || message.PrevHash != context.Block.PrevHash) return;
            // Log($"{nameof(OnPrepareRequestReceived)}: height={message.BlockIndex} view={message.ViewNumber} index={message.ValidatorIndex} tx={message.TransactionHashes.Length}");
            if (message.Timestamp <= context.PrevHeader.Timestamp || message.Timestamp > TimeProvider.Current.UtcNow.AddMilliseconds(8 * Blockchain.MillisecondsPerBlock).ToTimestampMS())
            {
                // Log($"Timestamp incorrect: {message.Timestamp}", LogLevel.Warning);
                return;
            }
            if (message.TransactionHashes.Any(p => NativeContract.Ledger.ContainsTransaction(context.Snapshot, p)))
            {
                // Log($"Invalid request: transaction already exists", LogLevel.Warning);
                return;
            }

            // Timeout extension: prepare request has been received with success
            // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
            // ExtendTimerByFactor(2);

            context.Block.Timestamp = message.Timestamp;
            context.Block.ConsensusData.Nonce = message.Nonce;
            context.TransactionHashes = message.TransactionHashes;
            context.Transactions = new Dictionary<UInt256, Transaction>();
            context.VerificationContext = new TransactionVerificationContext();
            for (int i = 0; i < context.PreparationPayloads.Length; i++)
                if (context.PreparationPayloads[i] != null)
                    if (!context.GetMessage<PrepareResponse>(context.PreparationPayloads[i]).PreparationHash.Equals(payload.Hash))
                        context.PreparationPayloads[i] = null;
            context.PreparationPayloads[message.ValidatorIndex] = payload;
            byte[] hashData = context.EnsureHeader().GetHashData();
            for (int i = 0; i < context.CommitPayloads.Length; i++)
                if (context.GetMessage(context.CommitPayloads[i])?.ViewNumber == context.ViewNumber)
                    if (!Crypto.VerifySignature(hashData, context.GetMessage<Commit>(context.CommitPayloads[i]).Signature, context.Validators[i]))
                        context.CommitPayloads[i] = null;

            if (context.TransactionHashes.Length == 0)
            {
                // There are no tx so we should act like if all the transactions were filled
                // CheckPrepareResponse();
                return;
            }

            Dictionary<UInt256, Transaction> mempoolVerified = Blockchain.Singleton.MemPool.GetVerifiedTransactions().ToDictionary(p => p.Hash);
            List<Transaction> unverified = new List<Transaction>();
            foreach (UInt256 hash in context.TransactionHashes)
            {
                if (mempoolVerified.TryGetValue(hash, out var tx))
                {
                    if (!AddTransaction(tx, false))
                        return;
                }
                else
                {
                    if (Blockchain.Singleton.MemPool.TryGetValue(hash, out tx))
                        unverified.Add(tx);
                }
            }
            foreach (Transaction tx in unverified)
                if (!AddTransaction(tx, true))
                    return;
            if (context.Transactions.Count < context.TransactionHashes.Length)
            {
                UInt256[] hashes = context.TransactionHashes.Where(i => !context.Transactions.ContainsKey(i)).ToArray();
                // taskManager.Tell(new TaskManager.RestartTasks
                // {
                //     Payload = InvPayload.Create(InventoryType.TX, hashes)
                // });
            }

            bool AddTransaction(Transaction tx, bool verify)
            {
                if (verify)
                {
                    VerifyResult result = tx.Verify(context.Snapshot, context.VerificationContext);
                    if (result != VerifyResult.Succeed)
                    {
                        // Log($"Rejected tx: {tx.Hash}, {result}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                        // RequestChangeView(result == VerifyResult.PolicyFail ? ChangeViewReason.TxRejectedByPolicy : ChangeViewReason.TxInvalid);
                        return false;
                    }
                }
                context.Transactions[tx.Hash] = tx;
                context.VerificationContext.AddTransaction(tx);
                return CheckPrepareResponse();
            }

            bool CheckPrepareResponse()
            {
                if (context.TransactionHashes.Length == context.Transactions.Count)
                {
                    // if we are the primary for this view, but acting as a backup because we recovered our own
                    // previously sent prepare request, then we don't want to send a prepare response.
                    if (context.IsPrimary || context.WatchOnly) return true;

                    // TODO: Replace reflection after https://github.com/neo-project/neo-modules/pull/503 is merged
                    // Check maximum block size via Native Contract policy
                    var expectedBlockSize = (int)(getExpectedBlockSizeMethodInfo.Value.Invoke(context, Array.Empty<object>()))!;
                    if (expectedBlockSize > NativeContract.Policy.GetMaxBlockSize(context.Snapshot))
                    {
                        // Log($"Rejected block: {context.Block.Index} The size exceed the policy", LogLevel.Warning);
                        // RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                        return false;
                    }

                    // TODO: Replace duplicate code for calculating blockSystemFee after https://github.com/neo-project/neo-modules/pull/503 is merged
                    // Check maximum block system fee via Native Contract policy
                    var blockSystemFee = context.Transactions.Values.Sum(u => u.SystemFee); 
                    if (blockSystemFee > NativeContract.Policy.GetMaxBlockSystemFee(context.Snapshot))
                    {
                        // Log($"Rejected block: {context.Block.Index} The system fee exceed the policy", LogLevel.Warning);
                        // RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                        return false;
                    }

                    // Timeout extension due to prepare response sent
                    // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
                    // ExtendTimerByFactor(2);

                    // Log($"Sending {nameof(PrepareResponse)}");
                    // localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakePrepareResponse() });
                    // CheckPreparations();
                }
                return true;
            }
        }

        static Lazy<MethodInfo> getExpectedBlockSizeMethodInfo = new Lazy<MethodInfo>(
            () => typeof(ConsensusContext).GetMethod("GetExpectedBlockSize", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("could not retrieve GetExpectedBlockSize methodInfo"));


        // TODO: remove if https://github.com/neo-project/neo/issues/2061 is fixed
        // this logic is lifted from ConsensusService.OnCommitReceived
        // Log, Timer,  CheckCommits logic has been commented out for offline consensus
        private static void OnCommitReceived(ConsensusContext context, ExtensiblePayload payload, Commit commit)
        {
            ref ExtensiblePayload existingCommitPayload = ref context.CommitPayloads[commit.ValidatorIndex];
            if (existingCommitPayload != null)
            {
                // if (existingCommitPayload.Hash != payload.Hash)
                //     Log($"Rejected {nameof(Commit)}: height={commit.BlockIndex} index={commit.ValidatorIndex} view={commit.ViewNumber} existingView={context.GetMessage(existingCommitPayload).ViewNumber}", LogLevel.Warning);
                return;
            }

            // Timeout extension: commit has been received with success
            // around 4*15s/M=60.0s/5=12.0s ~ 80% block time (for M=5)
            // ExtendTimerByFactor(4);

            if (commit.ViewNumber == context.ViewNumber)
            {
                // Log($"{nameof(OnCommitReceived)}: height={commit.BlockIndex} view={commit.ViewNumber} index={commit.ValidatorIndex} nc={context.CountCommitted} nf={context.CountFailed}");

                byte[]? hashData = context.EnsureHeader()?.GetHashData();
                if (hashData == null)
                {
                    existingCommitPayload = payload;
                }
                else if (Crypto.VerifySignature(hashData, commit.Signature, context.Validators[commit.ValidatorIndex]))
                {
                    existingCommitPayload = payload;
                    // CheckCommits();
                }
                return;
            }
            else
            {
                // Receiving commit from another view
                existingCommitPayload = payload;
            }
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

            var contracts = ExpressRpcServer.GetNep17Contracts(store).ToDictionary(c => c.ScriptHash);
            var balances = ExpressRpcServer.GetNep17Balances(store, address)
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

            var tx = NativeContract.Ledger.GetTransaction(Blockchain.Singleton.View, txHash);
            var log = ExpressAppLogsPlugin.TryGetAppLog(store, txHash);
            return Task.FromResult((tx, log != null ? RpcApplicationLog.FromJson(log) : null));
        }

        public Task<Block> GetBlockAsync(UInt256 blockHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));
            var block = NativeContract.Ledger.GetBlock(Blockchain.Singleton.View, blockHash);
            return Task.FromResult(block);
        }

        public Task<Block> GetBlockAsync(uint blockIndex)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));
            var block = NativeContract.Ledger.GetBlock(Blockchain.Singleton.View, blockIndex);
            return Task.FromResult(block);
        }

        public Task<Block> GetLatestBlockAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = Blockchain.Singleton.GetSnapshot();
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            var block = NativeContract.Ledger.GetBlock(snapshot, hash);
            return Task.FromResult(block);
        }

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            uint? height = NativeContract.Ledger.GetTransactionState(Blockchain.Singleton.View, txHash)?.BlockIndex;
            return height.HasValue
                ? Task.FromResult(height.Value)
                : Task.FromException<uint>(new Exception("Unknown transaction"));
        }

        public Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(UInt160 scriptHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = Blockchain.Singleton.GetSnapshot();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);

            if (contract != null)
            {
                byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
                IReadOnlyList<ExpressStorage> storages = snapshot.Find(prefix)
                    .Select(t => new ExpressStorage()
                    {
                        Key = t.Key.Key.ToHexString(),
                        Value = t.Value.Value.ToHexString(),
                        Constant = t.Value.IsConstant
                    })
                    .ToList();
                return Task.FromResult(storages);
            }

            return Task.FromResult<IReadOnlyList<ExpressStorage>>(Array.Empty<ExpressStorage>());
        }

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contractState = NativeContract.ContractManagement.GetContract(Blockchain.Singleton.View, scriptHash);
            if (contractState == null)
            {
                throw new Exception("Unknown contract");
            }
            return Task.FromResult(contractState.Manifest);
        }

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contracts = NativeContract.ContractManagement.ListContracts(Blockchain.Singleton.View)
                .OrderBy(c => c.Id)
                .Select(c => (c.Hash, c.Manifest))
                .ToList();

            return Task.FromResult<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>>(contracts);
        }

        public Task<IReadOnlyList<Nep17Contract>> ListNep17ContractsAsync()
        {
            return Task.FromResult<IReadOnlyList<Nep17Contract>>(
                ExpressRpcServer.GetNep17Contracts(store).ToList());
        }

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            using var snapshot = Blockchain.Singleton.GetSnapshot();
            var requests = NativeContract.Oracle.GetRequests(snapshot).ToList();
            return Task.FromResult<IReadOnlyList<(ulong, OracleRequest)>>(requests);
        }

        public Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, ECPoint[] oracleNodes)
        {
            using var snapshot = Blockchain.Singleton.GetSnapshot();

            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = OracleService.CreateResponseTx(snapshot, request, response, oracleNodes);
            if (tx == null) throw new Exception("Failed to create Oracle Response Tx");
            ExpressOracle.SignOracleResponseTransaction(chain, tx, oracleNodes);
            return SubmitTransactionAsync(tx);
        }

        public Task<bool> CreateCheckpointAsync(string checkPointPath)
        {
            if (store is RocksDbStore rocksDbStore)
            {
                var multiSigAccount = nodeWallet.GetAccounts().Single(a => a.IsMultiSigContract());
                rocksDbStore.CreateCheckpoint(checkPointPath, chain.Magic, multiSigAccount.ScriptHash.ToAddress());
                return Task.FromResult(false);
            }

            return Task.FromException<bool>(new Exception());
        }
    }
}
