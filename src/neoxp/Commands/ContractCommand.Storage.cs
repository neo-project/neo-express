// Copyright (C) 2015-2023 The Neo Project.
//
// ContractCommand.Storage.cs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.SmartContract.Manifest;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "storage", Description = "Display storage for specified contract")]
        internal class Storage
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Storage(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; }

            internal async Task WriteStoragesAsync(IExpressNode expressNode, TextWriter writer, IReadOnlyList<(UInt160 hash, ContractManifest)> contracts)
            {
                if (Json)
                {
                    using var jsonWriter = new JsonTextWriter(writer);

                    if (contracts.Count > 1)
                        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

                    for (int i = 0; i < contracts.Count; i++)
                    {
                        var storages = await expressNode.ListStoragesAsync(contracts[i].hash).ConfigureAwait(false);

                        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

                        await jsonWriter.WritePropertyNameAsync("script-hash").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(contracts[i].hash.ToString()).ConfigureAwait(false);

                        await jsonWriter.WritePropertyNameAsync("storages").ConfigureAwait(false);
                        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
                        for (int j = 0; j < storages.Count; j++)
                        {
                            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                            await jsonWriter.WritePropertyNameAsync("key").ConfigureAwait(false);
                            await jsonWriter.WriteValueAsync($"0x{storages[j].key}").ConfigureAwait(false);
                            await jsonWriter.WritePropertyNameAsync("value").ConfigureAwait(false);
                            await jsonWriter.WriteValueAsync($"0x{storages[j].value}").ConfigureAwait(false);
                            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                        }
                        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    }

                    if (contracts.Count > 1)
                        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                }
                else
                {
                    if (contracts.Count == 0)
                    {
                        await writer.WriteLineAsync($"No contracts found matching the name {Contract}").ConfigureAwait(false);
                    }
                    else
                    {
                        for (int i = 0; i < contracts.Count; i++)
                        {
                            var storages = await expressNode.ListStoragesAsync(contracts[i].hash).ConfigureAwait(false);
                            await writer.WriteLineAsync($"contract:  {contracts[i].hash}").ConfigureAwait(false);
                            for (int j = 0; j < storages.Count; j++)
                            {
                                await writer.WriteLineAsync($"  key:     0x{storages[j].key}").ConfigureAwait(false);
                                await writer.WriteLineAsync($"    value: 0x{storages[j].value}").ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var expressNode = chainManager.GetExpressNode();

                if (UInt160.TryParse(Contract, out var hash))
                {
                    await WriteStoragesAsync(expressNode, writer, new (UInt160, ContractManifest)[] { (hash, null!) }).ConfigureAwait(false);
                }
                else
                {
                    var contracts = await expressNode.ListContractsAsync(Contract).ConfigureAwait(false);
                    await WriteStoragesAsync(expressNode, writer, contracts).ConfigureAwait(false);
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out).ConfigureAwait(false);
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
