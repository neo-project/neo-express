// Copyright (C) 2015-2026 The Neo Project.
//
// CandidateCommand.List.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoExpress.Commands
{
    partial class CandidateCommand
    {
        [Command(Name = "list", Description = "List candidates")]
        internal class List
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly TransactionExecutorFactory txExecutorFactory;

            public List(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                    var list = await txExec.ListCandidatesAsync().ConfigureAwait(false);
                    await WriteCandidatesAsync(console.Out, list, Json).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex, true);
                    return 1;
                }
            }

            internal static async Task WriteCandidatesAsync(TextWriter writer, IReadOnlyList<TransactionExecutor.CandidateInfo> candidates, bool json)
            {
                if (json)
                {
                    var output = candidates.Select(candidate => new CandidateJson(
                        candidate.PublicKey,
                        candidate.Votes.ToString(CultureInfo.InvariantCulture)));
                    await writer.WriteAsync(JsonSerializer.Serialize(output, JsonOptions)).ConfigureAwait(false);
                }
                else
                {
                    foreach (var candidate in candidates)
                    {
                        await writer.WriteLineAsync($"{candidate.PublicKey,-67}{candidate.Votes.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                    }
                }
            }

            private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

            private sealed record CandidateJson(
                [property: JsonPropertyName("public-key")] string PublicKey,
                [property: JsonPropertyName("votes")] string Votes);
        }
    }
}
