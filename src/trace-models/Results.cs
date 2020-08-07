using System.Collections.Generic;
using MessagePack;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct Results : ITraceRecord
    {
        [Key(0)]
        public readonly VMState State;
        [Key(1)]
        public readonly long GasConsumed;
        [Key(2)]
        public readonly IReadOnlyList<StackItem> ResultStack;

        public Results(VMState state, long gasConsumed, IReadOnlyList<StackItem> resultStack)
        {
            this.State = state;
            this.GasConsumed = gasConsumed;
            this.ResultStack = resultStack;
        }
    }
}
