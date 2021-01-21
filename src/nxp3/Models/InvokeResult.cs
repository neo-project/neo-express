using System;
using Neo;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoExpress.Models
{
    public class InvokeResult
    {
        public VMState State { get; set; }

        public BigDecimal GasConsumed { get; set; }

        public Neo.VM.Types.StackItem[] Stack { get; set; } = Array.Empty<Neo.VM.Types.StackItem>();

        public Exception? Exception { get; set; }

        public static InvokeResult FromRpcInvokeResult(RpcInvokeResult result)
        {
            return new InvokeResult
            {
                State = result.State,
                GasConsumed = BigDecimal.Parse(result.GasConsumed, NativeContract.GAS.Decimals),
                Stack = result.Stack,
                Exception = string.IsNullOrEmpty(result.Exception)
                    ? null : new Exception(result.Exception)
            };
        }
    }
}
