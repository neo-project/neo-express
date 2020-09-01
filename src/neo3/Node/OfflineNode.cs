using Akka.Actor;
using Neo;
using Neo.Ledger;
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
using System.Threading.Tasks;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Neo3.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal class OfflineNode : IDisposable, IExpressNode
    {
        private readonly NeoSystem neoSystem;
        private ExpressStorageProvider storageProvider;
        private Wallet nodeWallet;
        private bool disposedValue;

        public OfflineNode(IStore store, ExpressWallet nodeWallet)
            : this(store, DevWallet.FromExpressWallet(nodeWallet))
        {
        }

        public OfflineNode(IStore store, Wallet nodeWallet)
        {
            storageProvider = new ExpressStorageProvider(store);
            neoSystem = new NeoSystem(storageProvider.Name);
            this.nodeWallet = nodeWallet;
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

            var ctx = new Neo.Consensus.ConsensusContext(nodeWallet, Blockchain.Singleton.Store);
            ctx.Reset(0);
            ctx.MakePrepareRequest();
            ctx.MakeCommit();
            var block = ctx.CreateBlock();
            var blockRelay = neoSystem.Blockchain.Ask<RelayResult>(block).Result;
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }

            return Task.FromResult(tx.Hash);
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
