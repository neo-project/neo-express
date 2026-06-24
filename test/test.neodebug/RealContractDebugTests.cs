// Copyright (C) 2015-2026 The Neo Project.
//
// RealContractDebugTests.cs file belongs to neo-express project and is free
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
using Neo.Extensions;
using Neo.IO;
using Neo.SmartContract;
using Neo.VM;
using NeoDebug.Neo3;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Script = Neo.VM.Script;
using StackItem = Neo.VM.Types.StackItem;

namespace test.neodebug
{
    // Exercises the debugger against a REAL nccs-compiled contract (the Registrar sample shipped as a test
    // fixture): its real .nef script and real .debug.json. This proves that debug-info parsing, source
    // breakpoint binding, and source-line mapping work against actual compiler output, not synthetic data.
    public class RealContractDebugTests
    {
        static Stream GetResource(string suffix)
        {
            var assembly = typeof(RealContractDebugTests).Assembly;
            var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            return assembly.GetManifestResourceStream(name)!;
        }

        static Script LoadRegistrarScript()
        {
            using var stream = GetResource("registrar.nef");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var reader = new MemoryReader(ms.ToArray());
            return reader.ReadSerializable<NefFile>().Script;
        }

        static DebugInfo LoadRegistrarDebugInfo()
        {
            using var stream = GetResource("registrar.debug.json");
            using var sr = new StreamReader(stream);
            return DebugInfo.Parse(JObject.Parse(sr.ReadToEnd()));
        }

        static byte[] Trace(UInt160 hash, Script script, params int[] instructionPointers)
        {
            var options = MessagePackSerializerOptions.Standard.WithResolver(TraceDebugResolver.Instance);
            using var ms = new MemoryStream();
            void Write(ITraceDebugRecord record) => MessagePackSerializer.Serialize(ms, record, options);

            Write(new ProtocolSettingsRecord(0x004F454Eu, 0x35));
            Write(new ScriptRecord(hash, script));
            foreach (var ip in instructionPointers)
            {
                var frame = new TraceRecord.StackFrame(hash, hash, ip, hasCatch: false,
                    Array.Empty<StackItem>(), Array.Empty<StackItem>(), Array.Empty<StackItem>(), Array.Empty<StackItem>());
                Write(new TraceRecord(VMState.NONE, 0, new[] { frame }));
            }
            Write(new TraceRecord(VMState.HALT, 0, Array.Empty<TraceRecord.StackFrame>()));
            return ms.ToArray();
        }

        [Fact]
        public void source_breakpoint_binds_to_a_real_compiled_contract_line()
        {
            var debugInfo = LoadRegistrarDebugInfo();
            var script = LoadRegistrarScript();
            var hash = debugInfo.ScriptHash;

            // The Registrar.Query method, with its real sequence points (addresses 131/132/143 -> lines 21/22/23).
            var method = debugInfo.Methods.First(m => m.Name == "Query");
            var sps = method.SequencePoints.OrderBy(sp => sp.Address).ToArray();
            var target = sps[2];
            var document = debugInfo.Documents[target.Document];

            var trace = Trace(hash, script, sps[0].Address, sps[1].Address, target.Address);
            var engine = new TraceReplayEngine(
                new TraceDebugReader(new MemoryStream(trace)),
                new[] { new KeyValuePair<UInt160, Script>(hash, script) });

            var events = new List<DebugEvent>();
            using var session = new DebugSession(engine, new[] { debugInfo }, Array.Empty<CastOperation>(), events.Add, DebugView.Source);

            session.Start();

            var breakpoints = session.SetBreakpoints(
                new Source { Name = Path.GetFileName(document), Path = document },
                new[] { new SourceBreakpoint(target.Start.Line) }).ToList();
            Assert.True(breakpoints[0].Verified, "the breakpoint should bind to a real sequence point");

            session.Continue();

            Assert.Contains(events.OfType<StoppedEvent>(), e => e.Reason == StoppedEvent.ReasonValue.Breakpoint);

            var frame = session.GetStackFrames(new StackTraceArguments { ThreadId = 1 }).First();
            Assert.Equal(target.Start.Line, frame.Line);
            Assert.Equal(document, frame.Source.Path);
            Assert.Equal("Query", frame.Name);
        }
    }
}
