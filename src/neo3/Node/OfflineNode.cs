using Akka.Actor;
using Neo;
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
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Designate;
using Neo.SmartContract.Native.Oracle;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Neo3.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal class OfflineNode : IDisposable, IExpressNode
    {
        private readonly NeoSystem neoSystem;
        private readonly ExpressStorageProvider storageProvider;
        private readonly Wallet nodeWallet;
        private readonly ExpressChain chain;
        private bool disposedValue;

        public OfflineNode(IStore store, ExpressWallet nodeWallet, ExpressChain chain)
            : this(store, DevWallet.FromExpressWallet(nodeWallet), chain)
        {
        }

        public OfflineNode(IStore store, Wallet nodeWallet, ExpressChain chain)
        {
            storageProvider = new ExpressStorageProvider(store);
            neoSystem = new NeoSystem(storageProvider.Name);
            this.nodeWallet = nodeWallet;
            this.chain = chain;
            _ = new ExpressAppLogsPlugin(store);

            ApplicationEngine.Log += OnLog;
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            var name = args.ScriptHash.ToString();
            Console.WriteLine($"{name} Log \"{args.Message}\" [{args.ScriptContainer.GetType().Name}]");
        }

        public Task<InvokeResult> InvokeAsync(Neo.VM.Script script)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using ApplicationEngine engine = ApplicationEngine.Run(script, container: null, gas: 20000000L);
            var result = new InvokeResult()
            {
                State = engine.State,
                Stack = engine.ResultStack.ToArray(),
                Exception = engine.FaultException,
                GasConsumed = new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)
            };
            return Task.FromResult(result);
        }

        public Task<UInt256> ExecuteAsync(ExpressChain chain, ExpressWalletAccount account, Neo.VM.Script script, decimal additionalGas = 0)
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

        public Task<UInt256> SubmitTransactionAsync(Transaction tx)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var txRelay = neoSystem.Blockchain.Ask<RelayResult>(tx).Result;
            if (txRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Transaction relay failed {txRelay.Result}");
            }

            var block = RunConsensus();
            var blockRelay = neoSystem.Blockchain.Ask<RelayResult>(block).Result;
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }

            return Task.FromResult(tx.Hash);
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
            var prepareRequest = primary.MakePrepareRequest();

            for (int x = 0; x < contexts.Length; x++)
            {
                if (contexts[x].MyIndex == primary.MyIndex) continue;
                OnPrepareRequestReceived(contexts[x], prepareRequest);
                var commit = contexts[x].MakeCommit();
                OnCommitReceived(primary, commit);
            }

            return primary.CreateBlock();
        }

        // TODO: remove if https://github.com/neo-project/neo/issues/2061 is fixed
        // this logic is lifted from ConsensusService.OnPrepareRequestReceived
        // Log, Timer, Task and CheckPrepare logic has been commented out for offline consensus
        private static void OnPrepareRequestReceived(ConsensusContext context, ConsensusPayload payload)
        {
            var message = (PrepareRequest)payload.ConsensusMessage;

            if (context.RequestSentOrReceived || context.NotAcceptingPayloadsDueToViewChanging) return;
            if (payload.ValidatorIndex != context.Block.ConsensusData.PrimaryIndex || message.ViewNumber != context.ViewNumber) return;
            // Log($"{nameof(OnPrepareRequestReceived)}: height={payload.BlockIndex} view={message.ViewNumber} index={payload.ValidatorIndex} tx={message.TransactionHashes.Length}");
            if (message.Timestamp <= context.PrevHeader.Timestamp || message.Timestamp > TimeProvider.Current.UtcNow.AddMilliseconds(8 * Blockchain.MillisecondsPerBlock).ToTimestampMS())
            {
                // Log($"Timestamp incorrect: {message.Timestamp}", LogLevel.Warning);
                return;
            }
            if (message.TransactionHashes.Any(p => context.Snapshot.ContainsTransaction(p)))
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
                    if (!context.PreparationPayloads[i].GetDeserializedMessage<PrepareResponse>().PreparationHash.Equals(payload.Hash))
                        context.PreparationPayloads[i] = null;
            context.PreparationPayloads[payload.ValidatorIndex] = payload;
            byte[] hashData = context.EnsureHeader().GetHashData();
            for (int i = 0; i < context.CommitPayloads.Length; i++)
                if (context.CommitPayloads[i]?.ConsensusMessage.ViewNumber == context.ViewNumber)
                    if (!Crypto.VerifySignature(hashData, context.CommitPayloads[i].GetDeserializedMessage<Commit>().Signature, context.Validators[i]))
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
                if (mempoolVerified.TryGetValue(hash, out Transaction tx))
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
            // if (context.Transactions.Count < context.TransactionHashes.Length)
            // {
            //     UInt256[] hashes = context.TransactionHashes.Where(i => !context.Transactions.ContainsKey(i)).ToArray();
            //     taskManager.Tell(new TaskManager.RestartTasks
            //     {
            //         Payload = InvPayload.Create(InventoryType.TX, hashes)
            //     });
            // }

            bool AddTransaction(Transaction tx, bool verify)
            {
                if (verify)
                {
                    VerifyResult result = tx.Verify(context.Snapshot, context.VerificationContext);
                    if (result == VerifyResult.PolicyFail)
                    {
                        // Log($"reject tx: {tx.Hash}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                        // RequestChangeView(ChangeViewReason.TxRejectedByPolicy);
                        return false;
                    }
                    else if (result != VerifyResult.Succeed)
                    {
                        // Log($"Invalid transaction: {tx.Hash}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                        // RequestChangeView(ChangeViewReason.TxInvalid);
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

                    // Check maximum block size via Native Contract policy
                    var expectedBlockSize = (int)(getExpectedBlockSizeMethodInfo.Value.Invoke(context, Array.Empty<object>()));
                    if (expectedBlockSize > NativeContract.Policy.GetMaxBlockSize(context.Snapshot))
                    {
                        // Log($"rejected block: {context.Block.Index} The size exceed the policy", LogLevel.Warning);
                        // RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                        return false;
                    }
                    // Check maximum block system fee via Native Contract policy
                    var expectedSystemFee = context.Transactions.Values.Sum(u => u.SystemFee);
                    if (expectedSystemFee > NativeContract.Policy.GetMaxBlockSystemFee(context.Snapshot))
                    {
                        // Log($"rejected block: {context.Block.Index} The system fee exceed the policy", LogLevel.Warning);
                        // RequestChangeView(ChangeViewReason.BlockRejectedByPolicy);
                        return false;
                    }

                    // Timeout extension due to prepare response sent
                    // around 2*15/M=30.0/5 ~ 40% block time (for M=5)
                    // ExtendTimerByFactor(2);

                    // Log($"send prepare response");
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
        private static void OnCommitReceived(ConsensusContext context, ConsensusPayload payload)
        {
            Commit commit = (Commit)payload.ConsensusMessage;

            ref ConsensusPayload existingCommitPayload = ref context.CommitPayloads[payload.ValidatorIndex];
            if (existingCommitPayload != null)
            {
                if (existingCommitPayload.Hash != payload.Hash)
                {
                    // Log($"{nameof(OnCommitReceived)}: different commit from validator! height={payload.BlockIndex} index={payload.ValidatorIndex} view={commit.ViewNumber} existingView={existingCommitPayload.ConsensusMessage.ViewNumber}", LogLevel.Warning);
                }
                return;
            }

            // Timeout extension: commit has been received with success
            // around 4*15s/M=60.0s/5=12.0s ~ 80% block time (for M=5)
            // ExtendTimerByFactor(4);

            if (commit.ViewNumber == context.ViewNumber)
            {
                // Log($"{nameof(OnCommitReceived)}: height={payload.BlockIndex} view={commit.ViewNumber} index={payload.ValidatorIndex} nc={context.CountCommitted} nf={context.CountFailed}");

                byte[]? hashData = context.EnsureHeader()?.GetHashData();
                if (hashData == null)
                {
                    existingCommitPayload = payload;
                }
                else if (Crypto.VerifySignature(hashData, commit.Signature, context.Validators[payload.ValidatorIndex]))
                {
                    existingCommitPayload = payload;
                    // CheckCommits();
                }
                return;
            }
            // Receiving commit from another view
            // Log($"{nameof(OnCommitReceived)}: record commit for different view={commit.ViewNumber} index={payload.ValidatorIndex} height={payload.BlockIndex}");
            existingCommitPayload = payload;
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
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task<(RpcNep5Balance balance, Nep5Contract contract)[]> GetBalancesAsync(UInt160 address)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contracts = ExpressRpcServer.GetNep5Contracts(Blockchain.Singleton.Store).ToDictionary(c => c.ScriptHash);
            var balances = ExpressRpcServer.GetNep5Balances(Blockchain.Singleton.Store, address)
                .Select(b => (
                    balance: new RpcNep5Balance
                    {
                        Amount = b.balance,
                        AssetHash = b.contract.ScriptHash,
                        LastUpdatedBlock = b.lastUpdatedBlock
                    }, 
                    contract: contracts.TryGetValue(b.contract.ScriptHash, out var value) 
                        ? value 
                        : Nep5Contract.Unknown(b.contract.ScriptHash)))
                .ToArray();
            return Task.FromResult(balances);
        }

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var tx = Blockchain.Singleton.GetTransaction(txHash);
            var log = ExpressAppLogsPlugin.TryGetAppLog(Blockchain.Singleton.Store, txHash);
            return Task.FromResult((tx, log != null ? RpcApplicationLog.FromJson(log) : null));
        }

        public Task<Block> GetBlockAsync(UInt256 blockHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            return Task.FromResult(Blockchain.Singleton.GetBlock(blockHash));
        }

        public Task<Block> GetBlockAsync(uint blockIndex)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            return Task.FromResult(Blockchain.Singleton.GetBlock(blockIndex));
        }

        public Task<Block> GetLatestBlockAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            return Task.FromResult(Blockchain.Singleton.GetBlock(Blockchain.Singleton.CurrentBlockHash));
        }

        public Task<uint> GetTransactionHeight(UInt256 txHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            uint? height = Blockchain.Singleton.View.Transactions.TryGet(txHash)?.BlockIndex;
            return height.HasValue
                ? Task.FromResult(height.Value)
                : Task.FromException<uint>(new Exception("Unknown transaction"));
        }

        public Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(UInt160 scriptHash)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contract = Blockchain.Singleton.View.Contracts.TryGet(scriptHash);
            if (contract != null)
            {
                IReadOnlyList<ExpressStorage> storages = Blockchain.Singleton.View.Storages.Find()
                    .Where(t => t.Key.Id == contract.Id)
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

            var contractState = Blockchain.Singleton.View.Contracts.TryGet(scriptHash);
            if (contractState == null)
            {
                throw new Exception("Unknown contract");
            }
            return Task.FromResult(contractState.Manifest);
        }

        public Task<IReadOnlyList<ContractManifest>> ListContractsAsync()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            var contracts = Blockchain.Singleton.View.Contracts.Find()
                    .OrderBy(t => t.Value.Id)
                    .Select(t => t.Value.Manifest)
                    .ToList();
            return Task.FromResult<IReadOnlyList<ContractManifest>>(contracts);
        }

        public Task<IReadOnlyList<Nep5Contract>> ListNep5ContractsAsync()
        {
            return Task.FromResult<IReadOnlyList<Nep5Contract>>(
                ExpressRpcServer.GetNep5Contracts(Blockchain.Singleton.Store).ToList());
        }

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
        {
            using var snapshot = Blockchain.Singleton.GetSnapshot();
            var requests = NativeContract.Oracle.GetRequests(snapshot).ToList();
            return Task.FromResult<IReadOnlyList<(ulong, OracleRequest)>>(requests);
        }

        public Task<UInt256> SubmitOracleResponseAsync(ExpressChain chain, OracleResponse response, ECPoint[] oracleNodes)
        {
            using var snapshot = Blockchain.Singleton.GetSnapshot();
            var tx = ExpressOracle.CreateResponseTx(snapshot, response);
            if (tx == null) throw new Exception("Failed to create Oracle Response Tx");
            ExpressOracle.SignOracleResponseTransaction(chain, tx, oracleNodes);
            return SubmitTransactionAsync(tx);
        }
    }
}
