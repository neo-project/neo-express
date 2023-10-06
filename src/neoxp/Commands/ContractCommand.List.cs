// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using System;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "list", Description = "List deployed contracts")]
        internal class List
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public List(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal async Task ExecuteAsync(System.IO.TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                using var expressNode = chainManager.GetExpressNode();

                var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);

                if (Json)
                {
                    using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
                    await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
                    foreach (var (hash, manifest) in contracts)
                    {
                        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                        await jsonWriter.WritePropertyNameAsync("name").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(manifest.Name).ConfigureAwait(false);
                        await jsonWriter.WritePropertyNameAsync("hash").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(hash.ToString()).ConfigureAwait(false);
                        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    }
                    await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                }
                else
                {
                    foreach (var (hash, manifest) in contracts)
                    {
                        await writer.WriteLineAsync($"{manifest.Name} ({hash})").ConfigureAwait(false);
                    }
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(Console.Out).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }

        }
    }
}
