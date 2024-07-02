// Copyright (C) 2015-2024 The Neo Project.
//
// TraceRecord.StackFrame.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using Neo.SmartContract;
using ExecutionContext = Neo.VM.ExecutionContext;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    public partial class TraceRecord
    {
        // Note, Neo 3 calculates the script hash used to identify a contract from the script binary at initial deployment + the address of the contract deployer
        // This enables a stable contract identifier, even as the contract is later updated.
        // However, debugging requires the SHA 256 hash of the script binary to tie a specific contract version to its associated debug info.

        // In TraceRecord:
        //   * ScriptHash property is the UInt160 value created on script deployment that identifies a contract (even after updates)
        //   * ScriptIdentifier is the SHA 256 hash of the script binary, needed to map scripts to debug info

        [MessagePackObject]
        public class StackFrame
        {
            [Key(0)]
            public readonly UInt160 ScriptHash;
            [Key(1)]
            public readonly UInt160 ScriptIdentifier;
            [Key(2)]
            public readonly int InstructionPointer;
            [Key(3)]
            public readonly bool HasCatch;
            [Key(4)]
            public readonly IReadOnlyList<StackItem> EvaluationStack;
            [Key(5)]
            public readonly IReadOnlyList<StackItem> LocalVariables;
            [Key(6)]
            public readonly IReadOnlyList<StackItem> StaticFields;
            [Key(7)]
            public readonly IReadOnlyList<StackItem> Arguments;

            public StackFrame(
                UInt160 scriptHash,
                UInt160 scriptIdentifier,
                int instructionPointer,
                bool hasCatch,
                IReadOnlyList<StackItem> evaluationStack,
                IReadOnlyList<StackItem> localVariables,
                IReadOnlyList<StackItem> staticFields,
                IReadOnlyList<StackItem> arguments)
            {
                ScriptHash = scriptHash;
                ScriptIdentifier = scriptIdentifier;
                InstructionPointer = instructionPointer;
                HasCatch = hasCatch;
                EvaluationStack = evaluationStack;
                LocalVariables = localVariables;
                StaticFields = staticFields;
                Arguments = arguments;
            }

            internal static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, ExecutionContext context, UInt160 scriptIdentifier)
            {
                var uint160Formatter = options.Resolver.GetFormatterWithVerify<UInt160>();
                var stackItemCollectionFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>();

                writer.WriteArrayHeader(8);
                uint160Formatter.Serialize(ref writer, context.GetScriptHash(), options);
                uint160Formatter.Serialize(ref writer, scriptIdentifier, options);
                writer.Write(context.InstructionPointer);
                writer.Write(context.TryStack?.Any(c => c.HasCatch) == true);
                stackItemCollectionFormatter.Serialize(ref writer, context.EvaluationStack, options);
                stackItemCollectionFormatter.Serialize(ref writer, Coalese(context.LocalVariables), options);
                stackItemCollectionFormatter.Serialize(ref writer, Coalese(context.StaticFields), options);
                stackItemCollectionFormatter.Serialize(ref writer, Coalese(context.Arguments), options);

                static IReadOnlyCollection<StackItem> Coalese(Neo.VM.Slot? slot) => (slot == null) ? Array.Empty<StackItem>() : slot;
            }
        }
    }
}
