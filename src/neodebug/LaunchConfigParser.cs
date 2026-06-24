// Copyright (C) 2015-2026 The Neo Project.
//
// LaunchConfigParser.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.SmartContract;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using System.Numerics;
using Script = Neo.VM.Script;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Builds a <see cref="DebugSession"/> from a DAP launch configuration: it loads the contract and its
    /// debug info, parses the requested return-type casts and source-file map, and selects the execution
    /// backend. A <c>trace-file</c> invocation produces a trace-replay session; an <c>operation</c>
    /// invocation deploys the contract into a fresh in-process chain and debugs the live call.
    /// </summary>
    internal static class LaunchConfigParser
    {
        // ProtocolSettings.Default has no committee, so a genesis block can't be created; use a
        // single-member committee for the throwaway in-process chain a live launch runs against.
        private static readonly ProtocolSettings Settings = ProtocolSettings.Default with
        {
            StandbyCommittee = new[] { ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", ECCurve.Secp256r1) },
            ValidatorsCount = 1,
        };

        public static async Task<IDebugSession> CreateDebugSessionAsync(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            IReadOnlyDictionary<string, JToken> config = launchArguments.ConfigurationProperties;
            var sourceFileMap = ParseSourceFileMap(config);
            var returnTypes = ParseReturnTypes(config);

            var program = config.TryGetValue("program", out var programToken) ? programToken.Value<string>() : null;
            if (string.IsNullOrEmpty(program))
                throw new JsonException("The launch configuration is missing the 'program' property.");

            var nef = await LoadNefFileAsync(program).ConfigureAwait(false);

            var debugInfos = new List<DebugInfo>();
            (await DebugInfo.LoadContractDebugInfoAsync(program, sourceFileMap).ConfigureAwait(false))
                .Switch(info => debugInfos.Add(info), _ => { });

            var engine = CreateEngine(config, program, nef);
            var debugView = defaultDebugView;
            if (debugView == DebugView.Source && debugInfos.Count == 0)
            {
                debugView = DebugView.Disassembly;
                sendEvent(new OutputEvent
                {
                    Category = OutputEvent.CategoryValue.Console,
                    Output = "Source debug information was not found; using disassembly view.\n",
                });
            }

            return new DebugSession(engine, debugInfos, returnTypes, sendEvent, debugView);
        }

        private static IApplicationEngine CreateEngine(IReadOnlyDictionary<string, JToken> config, string program, NefFile nef)
        {
            if (!config.TryGetValue("invocation", out var invocation) || invocation.Type != JTokenType.Object)
                throw new JsonException("The launch configuration is missing the 'invocation' property.");

            var seedContracts = new Dictionary<UInt160, Script>
            {
                [((Script)nef.Script).CalculateScriptHash()] = nef.Script,
            };

            var traceFile = invocation.Value<string>("trace-file");
            if (!string.IsNullOrEmpty(traceFile))
            {
                var reader = new TraceDebugReader(File.OpenRead(traceFile), leaveOpen: false, seedContracts);
                return new TraceReplayEngine(reader, seedContracts);
            }

            var operation = invocation.Value<string>("operation");
            if (string.IsNullOrEmpty(operation))
                throw new JsonException("The 'invocation' must specify a 'trace-file' to replay or an 'operation' to invoke.");

            return CreateLiveEngine(program, nef, invocation, operation);
        }

        // Deploys the contract into a fresh, single-block in-process chain and positions a live engine at the
        // requested invocation, so the same DebugSession can step it as it really executes.
        private static IApplicationEngine CreateLiveEngine(string program, NefFile nef, JToken invocation, string operation)
        {
            var manifest = ContractManifest.Parse(File.ReadAllText(Path.ChangeExtension(program, ".manifest.json")));

            var store = new MemoryStore();
            InitializeLedger(store);

            var deploySigner = new Signer { Account = UInt160.Zero, Scopes = WitnessScope.CalledByEntry };
            UInt160 contractHash;
            using (var snapshot = new StoreCache(store.GetSnapshot()))
            {
                contractHash = DeployContract(snapshot, deploySigner, nef, manifest);
                snapshot.Commit();
            }

            var paramParser = new ContractParameterParser(Settings.AddressVersion);
            var args = invocation["args"] is JToken argsJson
                ? paramParser.ParseParameters(argsJson).ToArray()
                : Array.Empty<ContractParameter>();

            byte[] invokeScript;
            using (var builder = new ScriptBuilder())
            {
                builder.EmitDynamicCall(contractHash, operation, args);
                invokeScript = builder.ToArray();
            }

            var signers = new[] { new Signer { Account = deploySigner.Account, Scopes = WitnessScope.CalledByEntry } };
            var tx = new Transaction
            {
                Version = 0,
                Nonce = 0,
                Script = invokeScript,
                Signers = signers,
                SystemFee = 0,
                NetworkFee = 0,
                ValidUntilBlock = Settings.MaxValidUntilBlockIncrement,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
            };

            var engineSnapshot = new StoreCache(store.GetSnapshot());
            var block = TestApplicationEngine.CreateDummyBlock(engineSnapshot, Settings);
            block.Transactions = new[] { tx };

            // A permissive witness checker: when stepping a contract under the debugger, treat all witnesses as
            // present so methods that CheckWitness do not fault for lack of a real signature.
            var engine = new DebugApplicationEngine(tx, engineSnapshot, Settings, block, _ => true);
            engine.LoadScript(invokeScript);
            return engine;
        }

        // Persists the genesis block so the ApplicationEngine ctor can read the ledger's current index.
        private static void InitializeLedger(IStore store)
        {
            using var snapshot = new StoreCache(store.GetSnapshot());
            var block = NeoSystem.CreateGenesisBlock(Settings);

            RunNativePersist(snapshot, block, TriggerType.OnPersist, ApplicationEngine.System_Contract_NativeOnPersist);
            RunNativePersist(snapshot, block, TriggerType.PostPersist, ApplicationEngine.System_Contract_NativePostPersist);

            snapshot.Commit();

            static void RunNativePersist(DataCache snapshot, Block block, TriggerType trigger, uint sysCall)
            {
                using var engine = ApplicationEngine.Create(trigger, null, snapshot, block, Settings, 0L);
                using var scriptBuilder = new ScriptBuilder();
                scriptBuilder.EmitSysCall(sysCall);
                engine.LoadScript(scriptBuilder.ToArray());
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException($"{trigger} operation failed", engine.FaultException);
            }
        }

        // Deploys a contract onto the snapshot — the logic of ContractManagement.Deploy, replayed directly so a
        // launch can stand up the contract without a real deployment transaction.
        private static UInt160 DeployContract(DataCache snapshot, Signer deploySigner, NefFile nef, ContractManifest manifest)
        {
            const byte Prefix_Contract = 8;

            Neo.SmartContract.Helper.Check(nef.Script, manifest.Abi);
            var hash = Neo.SmartContract.Helper.GetContractHash(deploySigner.Account, nef.CheckSum, manifest.Name);
            var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(hash);

            if (snapshot.Contains(key))
                return hash;
            if (!manifest.IsValid(ExecutionEngineLimits.Default, hash))
                throw new InvalidOperationException($"Invalid manifest for contract {hash}.");

            var contract = new ContractState
            {
                Hash = hash,
                Id = GetNextAvailableId(snapshot),
                Manifest = manifest,
                Nef = nef,
                UpdateCounter = 0,
            };
            snapshot.Add(key, new StorageItem(contract));

            var deployMethod = contract.Manifest.Abi.GetMethod("_deploy", 2);
            if (deployMethod is not null)
            {
                var tx = TestApplicationEngine.CreateTestTransaction(deploySigner);
                using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshot, null, Settings);
                var context = engine.LoadContract(contract, deployMethod, CallFlags.All);
                context.EvaluationStack.Push(StackItem.Null);
                context.EvaluationStack.Push(StackItem.False);
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException("_deploy operation failed", engine.FaultException);
            }

            return hash;

            static int GetNextAvailableId(DataCache snapshot)
            {
                const byte Prefix_NextAvailableId = 15;
                var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
                var item = snapshot.GetAndChange(key);
                int value = (int)(BigInteger)item;
                item.Add(1);
                return value;
            }
        }

        private static async Task<NefFile> LoadNefFileAsync(string path)
        {
            var buffer = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            var reader = new MemoryReader(buffer);
            return reader.ReadSerializable<NefFile>();
        }

        private static ImmutableDictionary<string, string> ParseSourceFileMap(IReadOnlyDictionary<string, JToken> config)
        {
            if (config.TryGetValue("sourceFileMap", out var json) && json.Type == JTokenType.Object)
            {
                return ((IEnumerable<KeyValuePair<string, JToken?>>)json).ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Value<string>() ?? string.Empty);
            }

            return ImmutableDictionary<string, string>.Empty;
        }

        private static IReadOnlyList<CastOperation> ParseReturnTypes(IReadOnlyDictionary<string, JToken> config)
        {
            if (config.TryGetValue("return-types", out var json))
            {
                var builder = ImmutableList.CreateBuilder<CastOperation>();
                foreach (var returnType in json)
                    builder.Add(DebugSession.CastOperations[returnType.Value<string>() ?? ""]);
                return builder.ToImmutable();
            }

            return ImmutableList<CastOperation>.Empty;
        }
    }
}
