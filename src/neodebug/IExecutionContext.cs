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
        /// <summary>Gets the instruction at <see cref="InstructionPointer"/>, or <see langword="null"/> if unavailable.</summary>
        Instruction? CurrentInstruction { get; }

        /// <summary>Gets the instruction pointer within <see cref="Script"/>.</summary>
        int InstructionPointer { get; }

        /// <summary>Gets the executing contract script hash for this frame.</summary>
        UInt160 ScriptHash { get; }

        /// <summary>Gets the recorded script identifier used to resolve this frame's bytecode.</summary>
        UInt160 ScriptIdentifier { get; }

        /// <summary>Gets the bytecode associated with this frame.</summary>
        Script Script { get; }

        /// <summary>Gets the method tokens associated with this frame when available.</summary>
        MethodToken[] Tokens { get; }

        /// <summary>Gets the evaluation stack captured at this step.</summary>
        IReadOnlyList<StackItem> EvaluationStack { get; }

        /// <summary>Gets the local variable slots captured at this step.</summary>
        IReadOnlyList<StackItem> LocalVariables { get; }

        /// <summary>Gets the static field slots captured at this step.</summary>
        IReadOnlyList<StackItem> StaticFields { get; }

        /// <summary>Gets the argument slots captured at this step.</summary>
        IReadOnlyList<StackItem> Arguments { get; }
    }
}
