// Copyright (C) 2015-2026 The Neo Project.
//
// DebugInfoBreakpointTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Models;
using System;
using Xunit;

namespace test.bctklib
{
    public class DebugInfoBreakpointTests
    {
        const string SourcePath = "/src/Contract.cs";

        // A single method whose statements live on source lines 5, 7, 7 and 9. Two sequence points
        // share line 7 (addresses 4 and 8) so the lowest-address tie-break can be exercised.
        static DebugInfo MakeDebugInfo(string documentPath = SourcePath) => new(
            ScriptHash: UInt160.Zero,
            DocumentRoot: "",
            Documents: new[] { documentPath },
            Methods: new[]
            {
                new DebugInfo.Method(
                    Id: "Contract.Run",
                    Namespace: "Contract",
                    Name: "Run",
                    Range: (0, 12),
                    ReturnType: "Integer",
                    Parameters: Array.Empty<DebugInfo.SlotVariable>(),
                    Variables: Array.Empty<DebugInfo.SlotVariable>(),
                    SequencePoints: new[]
                    {
                        new DebugInfo.SequencePoint(0, 0, (5, 9), (5, 20)),
                        new DebugInfo.SequencePoint(4, 0, (7, 9), (7, 18)),
                        new DebugInfo.SequencePoint(8, 0, (7, 20), (7, 30)),
                        new DebugInfo.SequencePoint(12, 0, (9, 9), (9, 17)),
                    }),
            },
            Events: Array.Empty<DebugInfo.Event>(),
            StaticVariables: Array.Empty<DebugInfo.SlotVariable>());

        [Fact]
        public void resolves_exact_line_to_its_sequence_point()
        {
            var ok = MakeDebugInfo().TryResolveBreakpoint(SourcePath, 5, out var bp);

            Assert.True(ok);
            Assert.Equal(0, bp.Address);
            Assert.Equal(5, bp.Line);
            Assert.Equal(9, bp.Column);
            Assert.Equal(0, bp.Document);
        }

        [Fact]
        public void snaps_forward_when_line_has_no_sequence_point()
        {
            // Line 6 carries no sequence point, so the breakpoint binds to the next line that does (7).
            var ok = MakeDebugInfo().TryResolveBreakpoint(SourcePath, 6, out var bp);

            Assert.True(ok);
            Assert.Equal(7, bp.Line);
            Assert.Equal(4, bp.Address);
        }

        [Fact]
        public void prefers_lowest_address_when_a_line_has_multiple_sequence_points()
        {
            var ok = MakeDebugInfo().TryResolveBreakpoint(SourcePath, 7, out var bp);

            Assert.True(ok);
            Assert.Equal(7, bp.Line);
            Assert.Equal(4, bp.Address);
        }

        [Fact]
        public void does_not_resolve_a_line_past_every_sequence_point()
        {
            var ok = MakeDebugInfo().TryResolveBreakpoint(SourcePath, 10, out var bp);

            Assert.False(ok);
            Assert.Equal(default, bp);
        }

        [Fact]
        public void matches_documents_by_file_name_when_the_full_path_differs()
        {
            var debugInfo = MakeDebugInfo(@"C:\build\agent\work\Contract.cs");

            var ok = debugInfo.TryResolveBreakpoint("/home/dev/project/Contract.cs", 5, out var bp);

            Assert.True(ok);
            Assert.Equal(0, bp.Address);
        }

        [Fact]
        public void does_not_resolve_against_an_unrelated_document()
        {
            var ok = MakeDebugInfo().TryResolveBreakpoint("/src/Other.cs", 5, out _);

            Assert.False(ok);
        }

        [Fact]
        public void resolve_breakpoint_returns_null_when_unresolved()
        {
            Assert.Null(MakeDebugInfo().ResolveBreakpoint(SourcePath, 10));

            var resolved = MakeDebugInfo().ResolveBreakpoint(SourcePath, 5);
            Assert.NotNull(resolved);
            Assert.Equal(0, resolved.Value.Address);
        }
    }
}
