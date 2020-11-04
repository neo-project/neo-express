using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Abstractions.Models;
using System;
using System.Threading.Tasks;

namespace NeoExpress.Neo3.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal class OnlineNode : IExpressNode
    {
        private readonly RpcClient rpcClient;

        public OnlineNode(ExpressConsensusNode node)
        {
            rpcClient = new RpcClient(node.GetUri().ToString());
        }

        public void Dispose()
        {
        }

        public async Task<UInt256> Execute(ExpressChain chain, ExpressWalletAccount account, Script script, decimal additionalGas = 0)
        {
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = account.GetScriptHashAsUInt160() } };
            var factory = new TransactionManagerFactory(rpcClient);
            var tm = await factory.MakeTransactionAsync(script, signers).ConfigureAwait(false);
            var tx = await tm
                .AddGas(additionalGas)
                .AddSignatures(chain, account)
                .SignAsync()
                .ConfigureAwait(false);

            return await rpcClient.SendRawTransactionAsync(tx);
        }

        public async Task<(BigDecimal gasConsumed, StackItem[] results)> Invoke(Script script)
        {
            var invokeResult = await rpcClient.InvokeScriptAsync(script).ConfigureAwait(false);
            var gasConsumed = BigDecimal.Parse(invokeResult.GasConsumed, NativeContract.GAS.Decimals);
            var results = invokeResult.Stack ?? Array.Empty<StackItem>();
            return (gasConsumed, results);
        }
    }
}
