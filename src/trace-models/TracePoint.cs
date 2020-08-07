using System.Collections.Generic;
using MessagePack;
using Neo.Ledger;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct TracePoint : ITraceRecord
    {
        [Key(0)]
        public readonly VMState State;
        [Key(1)]
        public readonly IReadOnlyList<StackFrame> StackFrames;
        [Key(2)]
        public readonly IReadOnlyDictionary<UInt160, IReadOnlyDictionary<byte[], StorageItem>> Storages;

        public TracePoint(VMState state, IReadOnlyList<StackFrame> stackFrames, IReadOnlyDictionary<UInt160, IReadOnlyDictionary<byte[], StorageItem>> storages)
        {
            State = state;
            StackFrames = stackFrames;
            Storages = storages;
        }
    }
}
