using Akka.Actor;
using Neo;
using Neo.Consensus;
using Neo.Cryptography;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
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
        }

        public Task<(BigDecimal gasConsumed, StackItem[] results)> Invoke(Neo.VM.Script script)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(OfflineNode));

            using ApplicationEngine engine = ApplicationEngine.Run(script, container: null, gas: 20000000L);
            var gasConsumed = new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals);
            var results = engine.ResultStack?.ToArray() ?? Array.Empty<StackItem>();
            return Task.FromResult((gasConsumed, results));
        }

        public Task<UInt256> Execute(ExpressChain chain, ExpressWalletAccount account, Neo.VM.Script script, decimal additionalGas = 0)
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

            return ExecuteTransaction(tx);
        }

        public Task<UInt256> ExecuteTransaction(Transaction tx)
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


        // this logic is lifted from ConsensusService.OnPrepareRequestReceived
        // Log, Timer, Task and CheckPrepare logic has been removed for offline consensus
        private static void OnPrepareRequestReceived(ConsensusContext context, ConsensusPayload payload)
        {
            var message = (PrepareRequest)payload.ConsensusMessage;

            if (context.RequestSentOrReceived || context.NotAcceptingPayloadsDueToViewChanging) return;
            if (payload.ValidatorIndex != context.Block.ConsensusData.PrimaryIndex || message.ViewNumber != context.ViewNumber) return;
            if (message.Timestamp <= context.PrevHeader.Timestamp || message.Timestamp > TimeProvider.Current.UtcNow.AddMilliseconds(8 * Blockchain.MillisecondsPerBlock).ToTimestampMS())
            {
                return;
            }
            if (message.TransactionHashes.Any(p => context.Snapshot.ContainsTransaction(p)))
            {
                return;
            }

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
                return;
            }

            Dictionary<UInt256, Transaction> mempoolVerified = Blockchain.Singleton.MemPool.GetVerifiedTransactions().ToDictionary(p => p.Hash);
            List<Transaction> unverified = new List<Transaction>();
            foreach (UInt256 hash in context.TransactionHashes)
            {
                if (mempoolVerified.TryGetValue(hash, out Transaction tx))
                {
                    if (!AddTransaction(context, tx, false))
                        return;
                }
                else
                {
                    if (Blockchain.Singleton.MemPool.TryGetValue(hash, out tx))
                        unverified.Add(tx);
                }
            }
            foreach (Transaction tx in unverified)
            {
                if (!AddTransaction(context, tx, true))
                    return;
            }

            static bool AddTransaction(ConsensusContext context, Transaction tx, bool verify)
            {
                if (verify)
                {
                    VerifyResult result = tx.Verify(context.Snapshot, context.VerificationContext);
                    if (result == VerifyResult.PolicyFail)
                    {
                        return false;
                    }
                    else if (result != VerifyResult.Succeed)
                    {
                        return false;
                    }
                }
                context.Transactions[tx.Hash] = tx;
                context.VerificationContext.AddTransaction(tx);
                return true;
            }
        }

        // this logic is lifted from ConsensusService.OnCommitReceived
        // Log, Timer and CheckCommits logic has been removed for offline consensus
        private static void OnCommitReceived(ConsensusContext context, ConsensusPayload payload)
        {
            Commit commit = (Commit)payload.ConsensusMessage;

            ref ConsensusPayload existingCommitPayload = ref context.CommitPayloads[payload.ValidatorIndex];
            if (existingCommitPayload != null)
            {
                return;
            }

            if (commit.ViewNumber == context.ViewNumber)
            {
                byte[]? hashData = context.EnsureHeader()?.GetHashData();
                if (hashData == null)
                {
                    existingCommitPayload = payload;
                }
                else if (Crypto.VerifySignature(hashData, commit.Signature, context.Validators[payload.ValidatorIndex]))
                {
                    existingCommitPayload = payload;
                }
                return;
            }

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
    }
}
