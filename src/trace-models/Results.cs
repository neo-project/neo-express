using System.Collections.Generic;
using MessagePack;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.Seattle.TraceDebug.Models
{
    [MessagePackObject]
    public struct Results : ITraceRecord
    {
        [Key(0)]
        public VMState State;
        [Key(1)]
        public long GasConsumed;
        [Key(2)]
        public IReadOnlyList<StackItem> ResultStack;
    }
}
