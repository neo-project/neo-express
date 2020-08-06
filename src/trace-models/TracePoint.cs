using System.Collections.Generic;
using MessagePack;
using Neo.Ledger;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.Seattle.TraceDebug.Models
{
    [MessagePackObject]
    public struct TracePoint : ITraceRecord
    {
        [Key(0)]
        public VMState State;
        [Key(1)]
        public IReadOnlyList<StackFrame> StackFrames;
        [Key(2)]
        public IReadOnlyDictionary<UInt160, IReadOnlyDictionary<byte[], StorageItem>> Storages;

        [MessagePackObject]
        public struct StackFrame
        {
            [Key(0)]
            public UInt160 ScriptHash;
            [Key(1)]
            public int InstructionPointer;
            [Key(2)]
            public IReadOnlyList<StackItem> EvaluationStack;
            [Key(3)]
            public IReadOnlyList<StackItem> LocalVariables;
            [Key(4)]
            public IReadOnlyList<StackItem> StaticFields;
            [Key(5)]
            public IReadOnlyList<StackItem> Arguments;
        }
    }
}
