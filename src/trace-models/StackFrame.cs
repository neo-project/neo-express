using System.Collections.Generic;
using MessagePack;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct StackFrame
    {
        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly int InstructionPointer;
        [Key(2)]
        public readonly IReadOnlyList<StackItem> EvaluationStack;
        [Key(3)]
        public readonly IReadOnlyList<StackItem> LocalVariables;
        [Key(4)]
        public readonly IReadOnlyList<StackItem> StaticFields;
        [Key(5)]
        public readonly IReadOnlyList<StackItem> Arguments;

        public StackFrame(UInt160 scriptHash, int instructionPointer, IReadOnlyList<StackItem> evaluationStack, IReadOnlyList<StackItem> localVariables, IReadOnlyList<StackItem> staticFields, IReadOnlyList<StackItem> arguments)
        {
            ScriptHash = scriptHash;
            InstructionPointer = instructionPointer;
            EvaluationStack = evaluationStack;
            LocalVariables = localVariables;
            StaticFields = staticFields;
            Arguments = arguments;
        }
    }
}
