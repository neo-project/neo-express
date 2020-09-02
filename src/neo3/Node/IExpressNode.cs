using Neo;
using Neo.VM;
using NeoExpress.Abstractions.Models;
using System;
using System.Threading.Tasks;

namespace NeoExpress.Neo3.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal interface IExpressNode : IDisposable
    {
        Task<UInt256> Execute(ExpressChain chain, ExpressWalletAccount account, Script script, decimal additionalGas = 0);
        Task<(BigDecimal gasConsumed, StackItem[] results)> Invoke(Script script);
    }
}
