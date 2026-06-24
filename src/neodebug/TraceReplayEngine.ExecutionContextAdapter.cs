// Copyright (C) 2015-2026 The Neo Project.
//
// TraceReplayEngine.ExecutionContextAdapter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.SmartContract;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    internal sealed partial class TraceReplayEngine
    {
        /// <summary>Presents a recorded <see cref="TraceRecord.StackFrame"/> as an <see cref="IExecutionContext"/>.</summary>
        private sealed class ExecutionContextAdapter : IExecutionContext
        {
            private readonly TraceRecord.StackFrame _frame;

            public ExecutionContextAdapter(TraceRecord.StackFrame frame, Script script)
            {
                _frame = frame;
                Script = script;
            }

            public Instruction? CurrentInstruction =>
                InstructionPointer >= 0 && InstructionPointer < Script.Length
                    ? Script.GetInstruction(InstructionPointer)
                    : null;
            public int InstructionPointer => _frame.InstructionPointer;
            public UInt160 ScriptHash => _frame.ScriptHash;
            public UInt160 ScriptIdentifier => _frame.ScriptIdentifier;
            public Script Script { get; }
            public MethodToken[] Tokens => Array.Empty<MethodToken>();
            public IReadOnlyList<StackItem> EvaluationStack => _frame.EvaluationStack;
            public IReadOnlyList<StackItem> LocalVariables => _frame.LocalVariables;
            public IReadOnlyList<StackItem> StaticFields => _frame.StaticFields;
            public IReadOnlyList<StackItem> Arguments => _frame.Arguments;
        }
    }
}
