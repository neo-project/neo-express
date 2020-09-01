using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.VM;
using NeoExpress.Abstractions.Models;
using System;

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

        public UInt256 Execute(ExpressChain chain, ExpressWalletAccount account, Script script)
        {
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = account.GetScriptHashAsUInt160() } };
            var tm = new TransactionManager(rpcClient)
                .MakeTransaction(script, signers)
                .AddSignatures(chain, account)
                .Sign();
            return rpcClient.SendRawTransaction(tm.Tx);
        }

        public StackItem[] Invoke(Script script)
        {
            var result = rpcClient.InvokeScript(script);
            return result.Stack ?? Array.Empty<StackItem>();
        }
    }
}
