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
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using Script = Neo.VM.Script;

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
            var operation = invocation.Value<string>("operation");
            if (!string.IsNullOrEmpty(traceFile) && !string.IsNullOrEmpty(operation))
                throw new JsonException("The 'invocation' must specify either 'trace-file' or 'operation', not both.");

            if (!string.IsNullOrEmpty(traceFile))
            {
                var reader = new TraceDebugReader(File.OpenRead(traceFile), leaveOpen: false, seedContracts);
                return new TraceReplayEngine(reader, seedContracts);
            }

            if (string.IsNullOrEmpty(operation))
                throw new JsonException("The 'invocation' must specify a 'trace-file' to replay or an 'operation' to invoke.");

            return CreateLiveEngine(config, program, nef, invocation, operation);
        }

        // Deploys the contract into a fresh, single-block in-process chain and positions a live engine at the
        // requested invocation, so the same DebugSession can step it as it really executes.
        private static IApplicationEngine CreateLiveEngine(IReadOnlyDictionary<string, JToken> config, string program, NefFile nef, JToken invocation, string operation)
        {
            var manifest = ContractManifest.Parse(File.ReadAllText(Path.ChangeExtension(program, ".manifest.json")));

            var (signers, deploySigner) = ParseSigners(config);

            var store = new MemoryStore();
            store.EnsureLedgerInitialized(Settings);

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

            var engine = new DebugApplicationEngine(tx, engineSnapshot, Settings, block, null);
            engine.LoadScript(invokeScript);
            return engine;
        }

        // Parses the launch configuration's signers. All signers use the ApplicationEngine's protocol witness
        // rules; when none are configured, the live launch uses the default zero-account signer.
        private static (Signer[] signers, Signer deploySigner) ParseSigners(IReadOnlyDictionary<string, JToken> config)
        {
            if (!config.TryGetValue("signers", out var signersJson))
                return CreateDefaultSigners();

            if (signersJson.Type != JTokenType.Array || !signersJson.HasValues)
                throw new JsonException("The 'signers' property must be a non-empty array of Neo N3 addresses.");

            if (signersJson.Count() > Transaction.MaxTransactionAttributes)
                throw new JsonException($"The 'signers' property cannot contain more than {Transaction.MaxTransactionAttributes} addresses.");

            var accounts = new HashSet<UInt160>();
            var signers = new List<Signer>();
            foreach (var token in signersJson)
            {
                if (token.Type != JTokenType.String)
                    throw new JsonException("Each entry in the 'signers' property must be a Neo N3 address string.");

                var account = ParseAddress(token.Value<string>()!);
                if (!accounts.Add(account))
                    throw new JsonException($"The 'signers' property contains the duplicate address '{token.Value<string>()}'.");

                signers.Add(new Signer { Account = account, Scopes = WitnessScope.CalledByEntry });
            }

            return (signers.ToArray(), signers[0]);

            static (Signer[] signers, Signer deploySigner) CreateDefaultSigners()
            {
                var deploySigner = new Signer { Account = UInt160.Zero, Scopes = WitnessScope.CalledByEntry };
                return (new[] { deploySigner }, deploySigner);
            }
        }

        private static UInt160 ParseAddress(string address)
        {
            try
            {
                return address.ToScriptHash(Settings.AddressVersion);
            }
            catch (FormatException ex)
            {
                throw new JsonException($"Invalid Neo N3 signer address '{address}'.", ex);
            }
        }

        // Deploys through ContractManagement so registry indexes, native calling context and the _deploy runtime
        // environment match a real deployment transaction.
        private static UInt160 DeployContract(DataCache snapshot, Signer deploySigner, NefFile nef, ContractManifest manifest)
        {
            Neo.SmartContract.Helper.Check(nef.Script, manifest.Abi);
            var hash = Neo.SmartContract.Helper.GetContractHash(deploySigner.Account, nef.CheckSum, manifest.Name);
            if (NativeContract.ContractManagement.GetContract(snapshot, hash) is not null)
                return hash;

            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(
                NativeContract.ContractManagement.Hash,
                "deploy",
                nef.ToArray(),
                manifest.ToJson().ToString(),
                new ContractParameter(ContractParameterType.Any));
            var tx = TestApplicationEngine.CreateTestTransaction(deploySigner);
            tx.Script = builder.ToArray();
            var block = TestApplicationEngine.CreateDummyBlock(snapshot, Settings);
            block.Transactions = new[] { tx };

            using (var engine = ApplicationEngine.Create(
                TriggerType.Application,
                tx,
                snapshot,
                block,
                Settings,
                ApplicationEngine.TestModeGas))
            {
                engine.LoadScript(tx.Script);
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException("Contract deployment failed", engine.FaultException);
            }

            return NativeContract.ContractManagement.GetContract(snapshot, hash) is not null
                ? hash
                : throw new InvalidOperationException($"Contract {hash} was not registered after deployment.");
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
