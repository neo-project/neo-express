// Copyright (C) 2015-2026 The Neo Project.
//
// DisassemblyManager.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.VM;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Renders a contract's NeoVM bytecode as annotated disassembly text and builds the line↔address maps that
    /// drive the disassembly debug view (stepping and breakpoints over raw instructions). Disassemblies are
    /// cached per script, keyed by a source reference handed back to the editor.
    /// </summary>
    internal class DisassemblyManager
    {
        public delegate bool TryGetScript(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);
        public delegate bool TryGetDebugInfo(UInt160 scriptHash, [MaybeNullWhen(false)] out DebugInfo debugInfo);

        public readonly struct Disassembly
        {
            public readonly string Name;
            public readonly string Source;
            public readonly int SourceReference;
            public readonly ImmutableDictionary<int, int> AddressMap;
            public readonly ImmutableDictionary<int, int> LineMap;

            public Disassembly(string name, string source, int sourceReference, ImmutableDictionary<int, int> addressMap, ImmutableDictionary<int, int> lineMap)
            {
                Name = name;
                Source = source;
                SourceReference = sourceReference;
                AddressMap = addressMap;
                LineMap = lineMap;
            }
        }

        private readonly ConcurrentDictionary<int, Disassembly> _disassemblies = new();
        private readonly TryGetScript _tryGetScript;
        private readonly TryGetDebugInfo _tryGetDebugInfo;

        public DisassemblyManager(TryGetScript tryGetScript, TryGetDebugInfo tryGetDebugInfo)
        {
            _tryGetScript = tryGetScript;
            _tryGetDebugInfo = tryGetDebugInfo;
        }

        public Disassembly GetDisassembly(IExecutionContext context, DebugInfo? debugInfo)
            => GetDisassembly(context.ScriptIdentifier, context.Script, debugInfo, context.Tokens);

        private Disassembly GetDisassembly(UInt160 scriptHash, Script script, DebugInfo? debugInfo, MethodToken[] tokens)
            => _disassemblies.GetOrAdd(scriptHash.GetHashCode(), sourceRef => ToDisassembly(sourceRef, scriptHash, script, debugInfo, tokens));

        public bool TryGetDisassembly(UInt160 scriptHash, out Disassembly disassembly)
        {
            if (_tryGetScript(scriptHash, out var script))
            {
                var debugInfo = _tryGetDebugInfo(scriptHash, out var foundDebugInfo) ? foundDebugInfo : null;
                disassembly = GetDisassembly(scriptHash, script, debugInfo, Array.Empty<MethodToken>());
                return true;
            }

            disassembly = default;
            return false;
        }

        public bool TryGetDisassembly(int sourceRef, out Disassembly disassembly)
            => _disassemblies.TryGetValue(sourceRef, out disassembly);

        private static Disassembly ToDisassembly(int sourceRef, UInt160 scriptHash, Script script, DebugInfo? debugInfo, MethodToken[] tokens)
        {
            var padString = script.GetInstructionAddressPadding();
            var sourceBuilder = new StringBuilder();
            var addressMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();
            var lineMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();

            var documents = debugInfo?.Documents
                .Select(path => (fileName: Path.GetFileName(path), lines: File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>()))
                .ToImmutableList() ?? ImmutableList<(string fileName, string[] lines)>.Empty;
            var methodStarts = debugInfo?.Methods.ToImmutableDictionary(m => m.Range.Start)
                ?? ImmutableDictionary<int, DebugInfo.Method>.Empty;
            var methodEnds = debugInfo?.Methods.ToImmutableDictionary(m => m.Range.End)
                ?? ImmutableDictionary<int, DebugInfo.Method>.Empty;
            var sequencePoints = debugInfo?.Methods.SelectMany(m => m.SequencePoints).ToImmutableDictionary(s => s.Address)
                ?? ImmutableDictionary<int, DebugInfo.SequencePoint>.Empty;

            var instructions = script.EnumerateInstructions().ToList();

            var line = 1;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (sourceBuilder.Length > 0)
                    sourceBuilder.Append('\n');

                if (methodStarts.TryGetValue(instructions[i].address, out var methodStart))
                {
                    sourceBuilder.AppendLine($"# Start Method {methodStart.Namespace}.{methodStart.Name}");
                    line++;
                }

                if (sequencePoints.TryGetValue(instructions[i].address, out var sp) && sp.Document < documents.Count)
                {
                    var doc = documents[sp.Document];
                    if (doc.lines.Length > sp.Start.Line - 1)
                    {
                        var srcLine = doc.lines[sp.Start.Line - 1];

                        if (sp.Start.Column > 1)
                            srcLine = srcLine.Substring(sp.Start.Column - 1);
                        if (sp.Start.Line == sp.End.Line && sp.End.Column > sp.Start.Column)
                            srcLine = srcLine.Substring(0, sp.End.Column - sp.Start.Column);

                        sourceBuilder.AppendLine($"# Code {doc.fileName} line {sp.Start.Line}: \"{srcLine.Trim()}\"");
                        line++;
                    }
                }

                AddSource(sourceBuilder, instructions[i].address, instructions[i].instruction, padString, tokens);
                addressMapBuilder.Add(instructions[i].address, line);
                lineMapBuilder.Add(line, instructions[i].address);
                line++;

                if (methodEnds.TryGetValue(instructions[i].address, out var methodEnd))
                {
                    sourceBuilder.Append($"\n# End Method {methodEnd.Namespace}.{methodEnd.Name}");
                    line++;
                }
            }

            return new Disassembly(
                scriptHash.ToString(),
                sourceBuilder.ToString(),
                sourceRef,
                addressMapBuilder.ToImmutable(),
                lineMapBuilder.ToImmutable());

            static void AddSource(StringBuilder sourceBuilder, int address, Instruction instruction, string padString, MethodToken[]? tokens)
            {
                sourceBuilder.Append($"{address.ToString(padString)} {instruction.OpCode}");
                if (!instruction.Operand.IsEmpty)
                    sourceBuilder.Append($" {instruction.GetOperandString()}");

                var comment = instruction.GetComment(address, tokens);
                if (comment.Length > 0)
                    sourceBuilder.Append($" # {comment}");
            }
        }
    }
}
