// Copyright (C) 2015-2026 The Neo Project.
//
// DebugSessionReplayTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using MessagePack.Resolvers;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.VM;
using NeoDebug.Neo3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Script = Neo.VM.Script;
using StackItem = Neo.VM.Types.StackItem;

namespace test.neodebug
{
    // Drives a real DebugSession over a synthetic trace + debug info to exercise the full replay flow:
    // start -> set a source breakpoint -> continue -> stop at the breakpoint -> stack frames -> variables
    // -> step backward (time-travel). No compiler is needed; the trace and debug info are built in memory.
    public class DebugSessionReplayTests
    {
        const string DocumentPath = "/src/Contract.cs";
        static readonly UInt160 ContractHash = UInt160.Parse("0x0000000000000000000000000000000000000001");

        // Single-byte opcodes so instruction pointers 0, 2 and 4 are all valid boundaries.
        static readonly Script ContractScript = new(new byte[]
        {
            (byte)OpCode.NOP, (byte)OpCode.NOP, (byte)OpCode.NOP,
            (byte)OpCode.NOP, (byte)OpCode.NOP, (byte)OpCode.NOP, (byte)OpCode.RET,
        });

        // Sequence points: address 0 -> line 5, address 2 -> line 6, address 4 -> line 7. One local "x".
        static DebugInfo SampleDebugInfo() => new(
            ContractHash, "", new[] { DocumentPath },
            new[]
            {
                new DebugInfo.Method(
                    Id: "Contract.Run", Namespace: "Contract", Name: "Run", Range: (0, 6), ReturnType: "Integer",
                    Parameters: Array.Empty<DebugInfo.SlotVariable>(),
                    Variables: new[] { new DebugInfo.SlotVariable("x", "Integer", 0) },
                    SequencePoints: new[]
                    {
                        new DebugInfo.SequencePoint(0, 0, (5, 1), (5, 10)),
                        new DebugInfo.SequencePoint(2, 0, (6, 1), (6, 10)),
                        new DebugInfo.SequencePoint(4, 0, (7, 1), (7, 10)),
                    }),
            },
            Array.Empty<DebugInfo.Event>(), Array.Empty<DebugInfo.SlotVariable>());

        static byte[] SampleTrace() => Serialize(
            new ProtocolSettingsRecord(0x004F454Eu, 0x35),
            new ScriptRecord(ContractHash, ContractScript),
            Trace(VMState.NONE, 10, 0),
            Trace(VMState.NONE, 20, 2),
            Trace(VMState.NONE, 30, 4),
            new TraceRecord(VMState.HALT, 40, Array.Empty<TraceRecord.StackFrame>()));

        static TraceRecord Trace(VMState state, long gas, int instructionPointer)
        {
            var frame = new TraceRecord.StackFrame(
                ContractHash, ContractHash, instructionPointer, hasCatch: false,
                evaluationStack: Array.Empty<StackItem>(),
                localVariables: new StackItem[] { new Neo.VM.Types.Integer(42) },
                staticFields: Array.Empty<StackItem>(),
                arguments: Array.Empty<StackItem>());
            return new TraceRecord(state, gas, new[] { frame });
        }

        static byte[] Serialize(params ITraceDebugRecord[] records)
        {
            var options = MessagePackSerializerOptions.Standard.WithResolver(TraceDebugResolver.Instance);
            using var ms = new MemoryStream();
            foreach (var record in records)
                MessagePackSerializer.Serialize<ITraceDebugRecord>(ms, record, options);
            return ms.ToArray();
        }

        private DebugSession NewSession(out List<DebugEvent> events)
        {
            var captured = new List<DebugEvent>();
            events = captured;
            var engine = new TraceReplayEngine(new TraceDebugReader(new MemoryStream(SampleTrace())));
            return new DebugSession(engine, new[] { SampleDebugInfo() }, Array.Empty<CastOperation>(), captured.Add, DebugView.Source);
        }

        static Source SourceFor() => new() { Name = "Contract.cs", Path = DocumentPath };

        [Fact]
        public void start_stops_at_a_source_sequence_point()
        {
            using var session = NewSession(out var events);

            session.Start();

            var stopped = events.OfType<StoppedEvent>().Last();
            Assert.Equal(StoppedEvent.ReasonValue.Step, stopped.Reason);

            var frame = session.GetStackFrames(new StackTraceArguments { ThreadId = 1 }).First();
            Assert.Equal(DocumentPath, frame.Source.Path);
            Assert.Equal(6, frame.Line); // step-in from entry advances to the next sequence point (line 6)
        }

        [Fact]
        public void continue_stops_at_a_source_breakpoint()
        {
            using var session = NewSession(out var events);
            session.Start();

            var bps = session.SetBreakpoints(SourceFor(), new[] { new SourceBreakpoint(7) }).ToList();
            Assert.True(bps[0].Verified);

            session.Continue();

            Assert.Contains(events.OfType<StoppedEvent>(), e => e.Reason == StoppedEvent.ReasonValue.Breakpoint);
            var frame = session.GetStackFrames(new StackTraceArguments { ThreadId = 1 }).First();
            Assert.Equal(7, frame.Line);
        }

        [Fact]
        public void scopes_expose_named_locals_from_debug_info()
        {
            using var session = NewSession(out _);
            session.Start();

            var scopes = session.GetScopes(new ScopesArguments { FrameId = 0 }).ToList();
            Assert.Contains(scopes, s => s.Name == "Variables");
            Assert.Contains(scopes, s => s.Name == "Storage");
            Assert.Contains(scopes, s => s.Name == "Engine");

            var variablesScope = scopes.First(s => s.Name == "Variables");
            var variables = session.GetVariables(new VariablesArguments { VariablesReference = variablesScope.VariablesReference }).ToList();
            Assert.Contains(variables, v => v.Name == "x" && v.Value == "42");
        }

        [Fact]
        public void step_back_is_time_travel_to_the_previous_line()
        {
            using var session = NewSession(out _);
            session.Start();
            session.SetBreakpoints(SourceFor(), new[] { new SourceBreakpoint(7) }).ToList();
            session.Continue();
            Assert.Equal(7, session.GetStackFrames(new StackTraceArguments { ThreadId = 1 }).First().Line);

            session.StepBack();

            Assert.Equal(6, session.GetStackFrames(new StackTraceArguments { ThreadId = 1 }).First().Line);
        }

        [Fact]
        public void exposes_a_single_main_thread()
        {
            using var session = NewSession(out _);
            var threads = session.GetThreads().ToList();
            Assert.Single(threads);
            Assert.Equal(1, threads[0].Id);
        }
    }
}
