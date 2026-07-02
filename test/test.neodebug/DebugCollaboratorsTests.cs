// Copyright (C) 2015-2026 The Neo Project.
//
// DebugCollaboratorsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.VM;
using NeoDebug.Neo3;
using System;
using System.Linq;
using Xunit;
using Script = Neo.VM.Script;

namespace test.neodebug
{
    public class DebugCollaboratorsTests
    {
        const string DocumentPath = "/src/Contract.cs";

        static Script SampleScript()
        {
            using var builder = new ScriptBuilder();
            builder.Emit(OpCode.PUSH1);
            builder.Emit(OpCode.PUSH2);
            builder.Emit(OpCode.ADD);
            builder.Emit(OpCode.RET);
            return new Script(builder.ToArray());
        }

        // A method spanning the whole script with one sequence point (address 2 -> source line 5).
        static DebugInfo SampleDebugInfo(UInt160 scriptHash) => new(
            scriptHash, "", new[] { DocumentPath },
            new[]
            {
                new DebugInfo.Method(
                    Id: "Contract.Run", Namespace: "Contract", Name: "Run", Range: (0, 3), ReturnType: "Integer",
                    Parameters: Array.Empty<DebugInfo.SlotVariable>(),
                    Variables: Array.Empty<DebugInfo.SlotVariable>(),
                    SequencePoints: new[] { new DebugInfo.SequencePoint(2, 0, (5, 1), (5, 10)) }),
            },
            Array.Empty<DebugInfo.Event>(), Array.Empty<DebugInfo.SlotVariable>());

        [Fact]
        public void disassembly_builds_consistent_address_and_line_maps()
        {
            var hash = UInt160.Parse("0x0000000000000000000000000000000000000001");
            var script = SampleScript();
            var debugInfo = SampleDebugInfo(hash);

            var manager = new DisassemblyManager(
                (UInt160 h, out Script? s) => { s = h == hash ? script : null; return s is not null; },
                (UInt160 h, out DebugInfo? d) => { d = h == hash ? debugInfo : null; return d is not null; });

            Assert.True(manager.TryGetDisassembly(hash, out var disassembly));
            Assert.Contains("PUSH1", disassembly.Source);
            Assert.Contains("ADD", disassembly.Source);

            // Every instruction address has a line, and the line maps back to the same address.
            foreach (var address in new[] { 0, 1, 2, 3 })
            {
                Assert.True(disassembly.AddressMap.TryGetValue(address, out var line));
                Assert.Equal(address, disassembly.LineMap[line]);
            }
        }

        [Fact]
        public void set_breakpoints_verifies_only_lines_with_sequence_points()
        {
            var debugInfo = SampleDebugInfo(UInt160.Zero);
            var manager = new BreakpointManager(EmptyDisassemblyManager(), new[] { debugInfo });

            var result = manager.SetBreakpoints(
                new Source { Name = "Contract.cs", Path = DocumentPath },
                new[] { new SourceBreakpoint(5), new SourceBreakpoint(99) }).ToList();

            Assert.Equal(2, result.Count);
            Assert.True(result[0].Verified);  // line 5 has a sequence point
            Assert.False(result[1].Verified); // line 99 does not
        }

        [Fact]
        public void check_breakpoint_matches_the_resolved_instruction_address()
        {
            var debugInfo = SampleDebugInfo(UInt160.Zero);
            var manager = new BreakpointManager(EmptyDisassemblyManager(), new[] { debugInfo });
            // SetBreakpoints is a deferred iterator; enumerate it so the breakpoints are recorded (as the adapter does).
            _ = manager.SetBreakpoints(new Source { Name = "Contract.cs", Path = DocumentPath }, new[] { new SourceBreakpoint(5) }).ToList();

            Assert.True(manager.CheckBreakpoint(UInt160.Zero, 2));  // line 5 resolved to address 2
            Assert.False(manager.CheckBreakpoint(UInt160.Zero, 3));
            Assert.False(manager.CheckBreakpoint(UInt160.Zero, null));
        }

        static DisassemblyManager EmptyDisassemblyManager() => new(
            (UInt160 h, out Script? s) => { s = null; return false; },
            (UInt160 h, out DebugInfo? d) => { d = null; return false; });
    }
}
