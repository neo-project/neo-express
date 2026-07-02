// Copyright (C) 2015-2026 The Neo Project.
//
// LaunchConfigParserTests.cs file belongs to neo-express project and is free
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
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Extensions;
using Neo.SmartContract;
using Neo.VM;
using NeoDebug.Neo3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Script = Neo.VM.Script;
using StackItem = Neo.VM.Types.StackItem;

namespace test.neodebug
{
    public class LaunchConfigParserTests
    {
        static readonly UInt160 ContractHash = UInt160.Parse("0x0000000000000000000000000000000000000001");

        static string WriteNefFile()
        {
            var nef = new NefFile
            {
                Compiler = "neodebug-test",
                Source = string.Empty,
                Tokens = Array.Empty<MethodToken>(),
                Script = new byte[] { (byte)OpCode.RET },
            };
            nef.CheckSum = NefFile.ComputeChecksum(nef);

            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.nef");
            File.WriteAllBytes(path, nef.ToArray());
            return path;
        }

        static string WriteTraceFile()
        {
            var script = new Script(new byte[] { (byte)OpCode.NOP, (byte)OpCode.RET });
            var frame = new TraceRecord.StackFrame(
                ContractHash, ContractHash, 0, hasCatch: false,
                evaluationStack: Array.Empty<StackItem>(),
                localVariables: Array.Empty<StackItem>(),
                staticFields: Array.Empty<StackItem>(),
                arguments: Array.Empty<StackItem>());

            var options = MessagePackSerializerOptions.Standard.WithResolver(TraceDebugResolver.Instance);
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.neo-trace");
            using (var stream = File.Create(path))
            {
                foreach (ITraceDebugRecord record in new ITraceDebugRecord[]
                {
                    new ProtocolSettingsRecord(0x004F454Eu, 0x35),
                    new ScriptRecord(ContractHash, script),
                    new TraceRecord(VMState.NONE, 10, new[] { frame }),
                    new TraceRecord(VMState.HALT, 20, Array.Empty<TraceRecord.StackFrame>()),
                })
                {
                    MessagePackSerializer.Serialize(stream, record, options);
                }
            }
            return path;
        }

        static LaunchArguments LaunchArgs(string program, JToken invocation, JToken? returnTypes = null)
        {
            var args = new LaunchArguments();
            args.ConfigurationProperties["program"] = program;
            args.ConfigurationProperties["invocation"] = invocation;
            if (returnTypes is not null)
                args.ConfigurationProperties["return-types"] = returnTypes;
            return args;
        }

        [Fact]
        public async Task builds_a_trace_replay_session_from_a_launch_config()
        {
            var args = LaunchArgs(
                WriteNefFile(),
                new JObject { ["trace-file"] = WriteTraceFile() },
                new JArray("int"));

            var session = await LaunchConfigParser.CreateDebugSessionAsync(args, _ => { }, DebugView.Source);

            Assert.NotNull(session);
            Assert.Single(session.GetThreads());
            (session as IDisposable)?.Dispose();
        }

        [Fact]
        public async Task missing_program_throws()
        {
            var args = new LaunchArguments();
            args.ConfigurationProperties["invocation"] = new JObject { ["trace-file"] = "x.neo-trace" };

            await Assert.ThrowsAsync<JsonException>(() =>
                LaunchConfigParser.CreateDebugSessionAsync(args, _ => { }, DebugView.Source));
        }

        [Fact]
        public async Task invocation_without_trace_file_or_operation_throws()
        {
            // Neither a trace-file (replay) nor an operation (live) — the invocation is unusable.
            var args = LaunchArgs(WriteNefFile(), new JObject { ["something-else"] = "x" });

            await Assert.ThrowsAsync<JsonException>(() =>
                LaunchConfigParser.CreateDebugSessionAsync(args, _ => { }, DebugView.Source));
        }
    }
}
