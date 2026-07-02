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
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Extensions;
using Neo.IO;
using Neo.SmartContract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using Script = Neo.VM.Script;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Builds a <see cref="DebugSession"/> from a DAP launch configuration: it loads the contract and its
    /// debug info, parses the requested return-type casts and source-file map, and selects the execution
    /// backend. A <c>trace-file</c> invocation produces a trace-replay session.
    /// </summary>
    internal static class LaunchConfigParser
    {
        public static async Task<IDebugSession> CreateDebugSessionAsync(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            IReadOnlyDictionary<string, JToken> config = launchArguments.ConfigurationProperties;
            var sourceFileMap = ParseSourceFileMap(config);
            var returnTypes = ParseReturnTypes(config);

            var program = config.TryGetValue("program", out var programToken) ? programToken.Value<string>() : null;
            if (string.IsNullOrEmpty(program))
                throw new JsonException("The launch configuration is missing the 'program' property.");

            var nef = await LoadNefFileAsync(program).ConfigureAwait(false);
            var seedContracts = new Dictionary<UInt160, Script>
            {
                [((Script)nef.Script).CalculateScriptHash()] = nef.Script,
            };

            var debugInfos = new List<DebugInfo>();
            (await DebugInfo.LoadContractDebugInfoAsync(program, sourceFileMap).ConfigureAwait(false))
                .Switch(info => debugInfos.Add(info), _ => { });

            var engine = CreateEngine(config, seedContracts);
            return new DebugSession(engine, debugInfos, returnTypes, sendEvent, defaultDebugView);
        }

        private static IApplicationEngine CreateEngine(IReadOnlyDictionary<string, JToken> config, IReadOnlyDictionary<UInt160, Script> seedContracts)
        {
            if (!config.TryGetValue("invocation", out var invocation) || invocation.Type != JTokenType.Object)
                throw new JsonException("The launch configuration is missing the 'invocation' property.");

            var traceFile = invocation.Value<string>("trace-file");
            if (!string.IsNullOrEmpty(traceFile))
            {
                var reader = new TraceDebugReader(File.OpenRead(traceFile), leaveOpen: false, seedContracts);
                return new TraceReplayEngine(reader, seedContracts);
            }

            throw new NotSupportedException(
                "This build of neodebug debugs recorded traces. Capture one with 'neotrace' and set 'invocation.trace-file' " +
                "in your launch configuration. Live (in-process) launch is planned as a follow-up.");
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
