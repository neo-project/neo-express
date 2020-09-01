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

        // TODO: Make these truly async when async RpcClient work is merged and available
        public Task<UInt256> Execute(ExpressChain chain, ExpressWalletAccount account, Script script, decimal additionalGas = 0)
        {
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = account.GetScriptHashAsUInt160() } };
            var tm = new TransactionManager(rpcClient)
                .MakeTransaction(script, signers)
                .AddGas(additionalGas)
                .AddSignatures(chain, account)
                .Sign();
            return Task.FromResult(rpcClient.SendRawTransaction(tm.Tx));
        }

        public Task<(BigDecimal gasConsumed, StackItem[] results)> Invoke(Script script)
        {
            var invokeResult = rpcClient.InvokeScript(script);
            var gasConsumed = BigDecimal.Parse(invokeResult.GasConsumed, NativeContract.GAS.Decimals);
            var results = invokeResult.Stack ?? Array.Empty<StackItem>();
            return Task.FromResult((gasConsumed, results));
        }
    }
}
