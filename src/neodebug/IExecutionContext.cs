// Copyright (C) 2015-2026 The Neo Project.
//
// IExecutionContext.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.SmartContract;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// A single frame of a debugged invocation stack, backend-agnostic: the same shape is served by the
    /// trace-replay engine (from a recorded frame) and the live engine (from a running VM context).
    /// </summary>
    internal interface IExecutionContext
    {
        Instruction? CurrentInstruction { get; }
        int InstructionPointer { get; }
        UInt160 ScriptHash { get; }
        UInt160 ScriptIdentifier { get; }
        Script Script { get; }
        MethodToken[] Tokens { get; }
        IReadOnlyList<StackItem> EvaluationStack { get; }
        IReadOnlyList<StackItem> LocalVariables { get; }
        IReadOnlyList<StackItem> StaticFields { get; }
        IReadOnlyList<StackItem> Arguments { get; }
    }
}
