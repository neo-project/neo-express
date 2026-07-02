// Copyright (C) 2015-2026 The Neo Project.
//
// DebugApplicationEngine.ExecutionContextAdapter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using Neo.VM;
using ExecutionContext = Neo.VM.ExecutionContext;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine
    {
        /// <summary>Presents a live NeoVM <see cref="ExecutionContext"/> as an <see cref="IExecutionContext"/>.</summary>
        private class ExecutionContextAdapter : IExecutionContext
        {
            private readonly ExecutionContext _context;

            public ExecutionContextAdapter(ExecutionContext context, IDictionary<UInt160, UInt160> scriptIdMap)
            {
                _context = context;
                ScriptHash = context.GetScriptHash();

                if (scriptIdMap.TryGetValue(ScriptHash, out var scriptHash))
                {
                    ScriptIdentifier = scriptHash;
                }
                else
                {
                    ScriptIdentifier = context.Script.CalculateScriptHash();
                    scriptIdMap[ScriptHash] = ScriptIdentifier;
                }
            }

            public Instruction? CurrentInstruction => _context.CurrentInstruction;
            public int InstructionPointer => _context.InstructionPointer;
            public IReadOnlyList<StackItem> EvaluationStack => _context.EvaluationStack;
            public IReadOnlyList<StackItem> LocalVariables => Coalesce(_context.LocalVariables);
            public IReadOnlyList<StackItem> StaticFields => Coalesce(_context.StaticFields);
            public IReadOnlyList<StackItem> Arguments => Coalesce(_context.Arguments);
            public Script Script => _context.Script;
            public MethodToken[] Tokens => _context.GetState<ExecutionContextState>()?.Contract?.Nef?.Tokens
                ?? Array.Empty<MethodToken>();

            public UInt160 ScriptHash { get; }
            public UInt160 ScriptIdentifier { get; }

            static IReadOnlyList<StackItem> Coalesce(Slot? slot) => slot ?? (IReadOnlyList<StackItem>)Array.Empty<StackItem>();
        }
    }
}
