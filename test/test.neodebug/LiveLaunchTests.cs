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
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoDebug.Neo3;
using Newtonsoft.Json;
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

        static string WriteContractFiles(byte[]? script = null, JObject? manifest = null, string returnType = "Integer")
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
            File.WriteAllText(basePath + ".manifest.json", manifest?.ToString() ?? ManifestJson.Replace("\"Integer\"", $"\"{returnType}\""));
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

        static async Task<List<OutputEvent>> RunSignedContract(byte[] script, UInt160 signer)
        {
            var events = new List<DebugEvent>();
            var args = LaunchArgs(WriteContractFiles(script, returnType: "Boolean"), new JObject { ["operation"] = "main" });
            args.ConfigurationProperties["signers"] = new JArray(signer.ToAddress(ProtocolSettings.Default.AddressVersion));

            var session = await LaunchConfigParser.CreateDebugSessionAsync(args, events.Add, DebugView.Source);
            session.Start();
            session.Continue();
            (session as IDisposable)?.Dispose();

            Assert.DoesNotContain(events.OfType<OutputEvent>(), e => e.Category == OutputEvent.CategoryValue.Stderr);
            return events.OfType<OutputEvent>().ToList();
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

        [Fact]
        public async Task live_launch_deploys_under_a_configured_signer()
        {
            var events = new List<DebugEvent>();
            var signer = UInt160.Parse("0x0000000000000000000000000000000000000001").ToAddress(ProtocolSettings.Default.AddressVersion);
            var args = LaunchArgs(WriteContractFiles(), new JObject { ["operation"] = "main" }, new JArray("int"));
            args.ConfigurationProperties["signers"] = new JArray(signer);

            // The contract deploys under (and the invocation is signed by) the configured account; main() does
            // not check witnesses, so it still runs to HALT returning 7.
            var session = await LaunchConfigParser.CreateDebugSessionAsync(args, events.Add, DebugView.Source);
            session.Start();
            session.Continue();

            Assert.Contains(events.OfType<OutputEvent>(), e => e.Output.Contains("Return: 7"));
            Assert.DoesNotContain(events.OfType<OutputEvent>(), e => e.Category == OutputEvent.CategoryValue.Stderr);

            (session as IDisposable)?.Dispose();
        }

        [Theory]
        [MemberData(nameof(InvalidSignerConfigurations))]
        public async Task live_launch_rejects_invalid_signer_configuration(JToken signers, string expectedMessage)
        {
            var args = LaunchArgs(WriteContractFiles(), new JObject { ["operation"] = "main" });
            args.ConfigurationProperties["signers"] = signers;

            var exception = await Assert.ThrowsAsync<JsonException>(() =>
                LaunchConfigParser.CreateDebugSessionAsync(args, _ => { }, DebugView.Source));

            Assert.Contains(expectedMessage, exception.Message);
        }

        public static IEnumerable<object[]> InvalidSignerConfigurations()
        {
            var address = UInt160.Zero.ToAddress(ProtocolSettings.Default.AddressVersion);
            yield return new object[] { new JValue(address), "non-empty array" };
            yield return new object[] { new JArray(), "non-empty array" };
            yield return new object[] { new JArray(1), "address string" };
            yield return new object[] { new JArray("not-an-address"), "Invalid Neo N3 signer address" };
            yield return new object[] { new JArray(address, address), "duplicate address" };
            yield return new object[]
            {
                new JArray(Enumerable.Range(0, Neo.Network.P2P.Payloads.Transaction.MaxTransactionAttributes + 1)
                    .Select(i => new UInt160(BitConverter.GetBytes(i).Concat(new byte[16]).ToArray())
                        .ToAddress(ProtocolSettings.Default.AddressVersion))),
                "cannot contain more than",
            };
        }

        [Fact]
        public async Task configured_signers_support_public_key_witnesses()
        {
            var key = new KeyPair(Enumerable.Repeat((byte)1, 32).ToArray());
            var signer = Contract.CreateSignatureRedeemScript(key.PublicKey).ToScriptHash();
            using var builder = new ScriptBuilder();
            builder.EmitPush(key.PublicKey.EncodePoint(true));
            builder.EmitSysCall(ApplicationEngine.System_Runtime_CheckWitness);
            builder.Emit(OpCode.RET);

            var output = await RunSignedContract(builder.ToArray(), signer);

            Assert.Contains(output, e => e.Output.Contains("Return: True"));
        }

        [Fact]
        public async Task configured_signers_honor_called_by_entry_scope()
        {
            var signer = UInt160.Parse("0x0000000000000000000000000000000000000001");
            using var builder = new ScriptBuilder();
            builder.Emit(OpCode.INITSLOT, new byte[] { 0, 1 });
            builder.Emit(OpCode.NEWARRAY0);
            builder.EmitPush((byte)CallFlags.All);
            builder.EmitPush("nested");
            builder.Emit(OpCode.LDARG0);
            builder.EmitSysCall(ApplicationEngine.System_Contract_Call);
            builder.Emit(OpCode.RET);
            var nestedOffset = builder.Length;
            builder.EmitPush(signer.ToArray());
            builder.EmitSysCall(ApplicationEngine.System_Runtime_CheckWitness);
            builder.Emit(OpCode.RET);

            var manifest = JObject.Parse(ManifestJson);
            manifest["abi"]!["methods"] = new JArray(
                new JObject
                {
                    ["name"] = "main",
                    ["parameters"] = new JArray(new JObject { ["name"] = "target", ["type"] = "Hash160" }),
                    ["returntype"] = "Boolean",
                    ["offset"] = 0,
                    ["safe"] = true,
                },
                new JObject
                {
                    ["name"] = "nested",
                    ["parameters"] = new JArray(),
                    ["returntype"] = "Boolean",
                    ["offset"] = nestedOffset,
                    ["safe"] = true,
                });
            var program = WriteContractFiles(builder.ToArray(), manifest: manifest);
            var reader = new MemoryReader(File.ReadAllBytes(program));
            var nef = reader.ReadSerializable<NefFile>();
            var contractHash = Neo.SmartContract.Helper.GetContractHash(signer, nef.CheckSum, "LiveTest");
            var events = new List<DebugEvent>();
            var args = LaunchArgs(program, new JObject
            {
                ["operation"] = "main",
                ["args"] = new JArray($"#{contractHash}"),
            });
            args.ConfigurationProperties["signers"] = new JArray(signer.ToAddress(ProtocolSettings.Default.AddressVersion));

            var session = await LaunchConfigParser.CreateDebugSessionAsync(args, events.Add, DebugView.Source);
            session.Start();
            session.Continue();
            (session as IDisposable)?.Dispose();

            Assert.DoesNotContain(events.OfType<OutputEvent>(), e => e.Category == OutputEvent.CategoryValue.Stderr);
            Assert.Contains(events.OfType<OutputEvent>(), e => e.Output.Contains("Return: False"));
        }

        [Fact]
        public async Task configured_signers_preserve_calling_contract_witnesses()
        {
            var signer = UInt160.Parse("0x0000000000000000000000000000000000000001");
            using var builder = new ScriptBuilder();
            builder.EmitSysCall(ApplicationEngine.System_Runtime_GetCallingScriptHash);
            builder.EmitSysCall(ApplicationEngine.System_Runtime_CheckWitness);
            builder.Emit(OpCode.RET);

            var output = await RunSignedContract(builder.ToArray(), signer);

            Assert.Contains(output, e => e.Output.Contains("Return: True"));
        }
    }
}
