// Copyright (C) 2015-2026 The Neo Project.
//
// LiveLaunchTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Extensions;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoDebug.Neo3;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace test.neodebug
{
    // Drives the live (in-process) launch path: a deployed contract is invoked and stepped to HALT, with no
    // recorded trace. The contract is built in memory so no compiler is required.
    public class LiveLaunchTests
    {
        // A one-method contract: main() returns the integer 7 (PUSH7; RET at offset 0).
        const string ManifestJson = @"{
            ""name"": ""LiveTest"",
            ""groups"": [],
            ""features"": {},
            ""supportedstandards"": [],
            ""abi"": { ""methods"": [ { ""name"": ""main"", ""parameters"": [], ""returntype"": ""Integer"", ""offset"": 0, ""safe"": true } ], ""events"": [] },
            ""permissions"": [ { ""contract"": ""*"", ""methods"": ""*"" } ],
            ""trusts"": [],
            ""extra"": null
        }";

        static string WriteContractFiles(byte[]? script = null, JObject? manifest = null)
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            var nef = new NefFile
            {
                Compiler = "neodebug-test",
                Source = string.Empty,
                Tokens = Array.Empty<MethodToken>(),
                Script = script ?? new byte[] { (byte)OpCode.PUSH7, (byte)OpCode.RET },
            };
            nef.CheckSum = NefFile.ComputeChecksum(nef);

            File.WriteAllBytes(basePath + ".nef", nef.ToArray());
            File.WriteAllText(basePath + ".manifest.json", manifest?.ToString() ?? ManifestJson);
            return basePath + ".nef";
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
        public async Task deploys_and_runs_a_live_invocation_to_halt()
        {
            var events = new List<DebugEvent>();
            var args = LaunchArgs(WriteContractFiles(), new JObject { ["operation"] = "main" }, new JArray("int"));

            var session = await LaunchConfigParser.CreateDebugSessionAsync(args, events.Add, DebugView.Source);
            Assert.Single(session.GetThreads());

            // With no debug info, Start stops in disassembly at the entry script; continue runs to completion.
            session.Start();
            session.Continue();

            Assert.Contains(events.OfType<ExitedEvent>(), _ => true);
            Assert.Contains(events.OfType<OutputEvent>(), e => e.Output.Contains("Return: 7"));
            Assert.DoesNotContain(events.OfType<OutputEvent>(), e => e.Category == OutputEvent.CategoryValue.Stderr);

            (session as IDisposable)?.Dispose();
        }

        [Fact]
        public async Task live_launch_uses_protocol_witness_checks()
        {
            var account = UInt160.Parse("0x0000000000000000000000000000000000000001");
            using var builder = new ScriptBuilder();
            builder.EmitPush(account.ToArray());
            builder.EmitSysCall(ApplicationEngine.System_Runtime_CheckWitness);
            builder.Emit(OpCode.RET);
            var manifest = JObject.Parse(ManifestJson);
            manifest["abi"]!["methods"]![0]!["returntype"] = "Boolean";
            var events = new List<DebugEvent>();
            var args = LaunchArgs(WriteContractFiles(builder.ToArray(), manifest), new JObject { ["operation"] = "main" });

            using var session = await LaunchConfigParser.CreateDebugSessionAsync(args, events.Add, DebugView.Source);
            session.Start();
            session.Continue();

            Assert.Contains(events.OfType<OutputEvent>(), e => e.Output.Contains("Return: False"));
            Assert.DoesNotContain(events.OfType<OutputEvent>(), e => e.Category == OutputEvent.CategoryValue.Stderr);
        }

        [Fact]
        public async Task live_launch_uses_native_deployment_context_and_registry()
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "getContractById", 1);
            builder.Emit(OpCode.ISNULL, OpCode.NOT, OpCode.RET);
            var deployOffset = builder.Length;
            builder.Emit(OpCode.INITSLOT, new byte[] { 0, 2 });
            builder.EmitSysCall(ApplicationEngine.System_Runtime_GetCallingScriptHash);
            builder.EmitPush(NativeContract.ContractManagement.Hash);
            builder.Emit(OpCode.EQUAL, OpCode.ASSERT);
            builder.EmitSysCall(ApplicationEngine.System_Runtime_GetTime);
            builder.Emit(OpCode.DROP, OpCode.RET);

            var manifest = JObject.Parse(ManifestJson);
            manifest["abi"]!["methods"] = new JArray(
                new JObject
                {
                    ["name"] = "main",
                    ["parameters"] = new JArray(),
                    ["returntype"] = "Boolean",
                    ["offset"] = 0,
                    ["safe"] = true,
                },
                new JObject
                {
                    ["name"] = "_deploy",
                    ["parameters"] = new JArray(
                        new JObject { ["name"] = "data", ["type"] = "Any" },
                        new JObject { ["name"] = "update", ["type"] = "Boolean" }),
                    ["returntype"] = "Void",
                    ["offset"] = deployOffset,
                    ["safe"] = false,
                });
            var events = new List<DebugEvent>();
            var args = LaunchArgs(WriteContractFiles(builder.ToArray(), manifest), new JObject { ["operation"] = "main" });

            using var session = await LaunchConfigParser.CreateDebugSessionAsync(args, events.Add, DebugView.Source);
            session.Start();
            session.Continue();

            Assert.Contains(events.OfType<OutputEvent>(), e => e.Output.Contains("Return: True"));
            Assert.DoesNotContain(events.OfType<OutputEvent>(), e => e.Category == OutputEvent.CategoryValue.Stderr);
        }
    }
}
