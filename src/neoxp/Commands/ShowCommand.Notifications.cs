// Copyright (C) 2015-2024 The Neo Project.
//
// ShowCommand.Notifications.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("notifications", Description = "Shows contract notifications in JSON format")]
        internal class Notifications
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Notifications(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "Limit shown notifications to the specified contract")]
            internal string Contract { get; init; } = string.Empty;

            [Option("-n|--count", Description = "Limit number of shown notifications")]
            internal uint? Count { get; init; }

            [Option(Description = "Limit shown notifications to specified event name")]
            internal string EventName { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();

                    var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
                    var contractMap = contracts.ToDictionary(c => c.hash, c => c.manifest.Name);

                    IReadOnlySet<UInt160>? contractFilter = null;
                    if (!string.IsNullOrEmpty(Contract))
                    {
                        if (UInt160.TryParse(Contract, out var _contract))
                        {
                            contractFilter = new HashSet<UInt160>() { _contract };
                        }
                        else
                        {
                            contractFilter = contracts
                                .Where(c => Contract.Equals(c.manifest.Name, StringComparison.OrdinalIgnoreCase))
                                .Select(c => c.hash)
                                .ToHashSet();
                            if (contractFilter.Count == 0)
                                throw new Exception($"Couldn't resolve {Contract} contract");
                        }
                    }

                    IReadOnlySet<string>? eventFilter = null;
                    if (!string.IsNullOrEmpty(EventName))
                    {
                        eventFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EventName };
                    }

                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    writer.WriteStartArray();
                    var count = 0;
                    await foreach (var (blockIndex, notification) in expressNode.EnumerateNotificationsAsync(contractFilter, eventFilter))
                    {
                        if (Count.HasValue && count++ >= Count.Value)
                            break;

                        writer.WriteStartObject();
                        writer.WritePropertyName("block-index");
                        writer.WriteValue(blockIndex);
                        writer.WritePropertyName("script-hash");
                        writer.WriteValue(notification.ScriptHash.ToString());
                        if (contractMap.TryGetValue(notification.ScriptHash, out var name))
                        {
                            writer.WritePropertyName("contract-name");
                            writer.WriteValue(name);
                        }
                        writer.WritePropertyName("event-name");
                        writer.WriteValue(notification.EventName);
                        writer.WritePropertyName("state");
                        writer.WriteJson(Neo.VM.Helper.ToJson(notification.State)["value"]);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

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
