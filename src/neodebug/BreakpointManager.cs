// Copyright (C) 2015-2026 The Neo Project.
//
// BreakpointManager.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using System.Collections.Immutable;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Translates source-level breakpoints into the set of NeoVM instruction addresses to stop on, and checks
    /// the running instruction pointer against them. Source documents are matched to sequence points; a
    /// document named by a script hash is matched against that contract's disassembly line map instead.
    /// </summary>
    internal class BreakpointManager
    {
        private readonly DisassemblyManager _disassemblyManager;
        private readonly IReadOnlyList<DebugInfo> _debugInfoList;
        private readonly Dictionary<UInt160, ImmutableHashSet<int>> _breakpointCache = new();
        private readonly Dictionary<string, IReadOnlyList<SourceBreakpoint>> _sourceBreakpointMap = new();

        public BreakpointManager(DisassemblyManager disassemblyManager, IReadOnlyList<DebugInfo> debugInfoList)
        {
            _disassemblyManager = disassemblyManager;
            _debugInfoList = debugInfoList;
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            _breakpointCache.Clear();
            _sourceBreakpointMap[source.Path] = sourceBreakpoints;

            if (UInt160.TryParse(source.Name, out var scriptHash))
            {
                var lineMap = _disassemblyManager.TryGetDisassembly(scriptHash, out var disassembly)
                    ? disassembly.LineMap : ImmutableDictionary<int, int>.Empty;

                foreach (var sbp in sourceBreakpoints)
                {
                    yield return new Breakpoint()
                    {
                        Verified = lineMap.TryGetValue(sbp.Line, out _),
                        Column = sbp.Column,
                        Line = sbp.Line,
                        Source = source,
                    };
                }
            }
            else
            {
                var sequencePoints = _debugInfoList
                    .SelectMany(d => d.Methods.SelectMany(m => m.SequencePoints).Select(sp => (d, sp)))
                    .Where(t => t.sp.PathEquals(t.d, source.Path))
                    .Select(t => t.sp)
                    .ToImmutableList();

                foreach (var sbp in sourceBreakpoints)
                {
                    yield return new Breakpoint()
                    {
                        Verified = sequencePoints.Any(sp => sp.Start.Line == sbp.Line),
                        Column = sbp.Column,
                        Line = sbp.Line,
                        Source = source,
                    };
                }
            }
        }

        private IReadOnlySet<int> GetBreakpoints(UInt160 scriptHash)
        {
            if (!_breakpointCache.TryGetValue(scriptHash, out var breakpoints))
            {
                var builder = ImmutableHashSet.CreateBuilder<int>();

                foreach (var kvp in _sourceBreakpointMap)
                {
                    if (UInt160.TryParse(kvp.Key, out var sourceScriptHash))
                    {
                        if (sourceScriptHash == scriptHash)
                        {
                            var lineMap = _disassemblyManager.TryGetDisassembly(scriptHash, out var disassembly)
                                ? disassembly.LineMap : ImmutableDictionary<int, int>.Empty;

                            foreach (var sbp in kvp.Value)
                            {
                                if (lineMap.TryGetValue(sbp.Line, out var address))
                                    builder.Add(address);
                            }
                        }
                    }
                    else
                    {
                        foreach (var debugInfo in _debugInfoList)
                        {
                            IReadOnlyList<DebugInfo.SequencePoint> sequencePoints = debugInfo.Methods
                                .SelectMany(m => m.SequencePoints)
                                .Where(sp => sp.PathEquals(debugInfo, kvp.Key))
                                .ToList();

                            foreach (var sbp in kvp.Value)
                            {
                                if (sequencePoints.TryFind(sp => sp.Start.Line == sbp.Line, out var found))
                                    builder.Add(found.Address);
                            }
                        }
                    }
                }

                breakpoints = builder.ToImmutable();
                _breakpointCache[scriptHash] = breakpoints;
            }

            return breakpoints;
        }

        public bool CheckBreakpoint(UInt160 scriptHash, int? instructionPointer)
            => instructionPointer.HasValue && GetBreakpoints(scriptHash).Contains(instructionPointer.Value);
    }
}
