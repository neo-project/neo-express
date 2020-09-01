using Neo;
using Neo.VM;
using NeoExpress.Abstractions.Models;
using System;

namespace NeoExpress.Neo3.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal interface IExpressNode : IDisposable
    {
        UInt256 Execute(ExpressChain chain, ExpressWalletAccount account, Script script, decimal additionalGas = 0);
        (BigDecimal gasConsumed, StackItem[] results) Invoke(Script script);
    }
}
