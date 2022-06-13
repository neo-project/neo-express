using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            readonly IExpressChain chain;

            public Notifications(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Notifications(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Option(Description = "Limit shown notifications to the specified contract")]
            internal string Contract { get; init; } = string.Empty;

            [Option("-n|--count", Description = "Limit number of shown notifications")]
            internal uint? Count { get; init; }

            [Option(Description = "Limit shown notifications to specified event name")]
            internal string EventName { get; init; } = string.Empty;


            internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
                var contractMap = contracts.ToDictionary(c => c.hash, c => c.manifest.Name);

                IReadOnlySet<UInt160>? contractFilter = null;
                if (!string.IsNullOrEmpty(Contract))
                {
                    if (UInt160.TryParse(Contract, out var hash))
                    {
                        contractFilter = new HashSet<UInt160>() { hash };
                    }
                    else
                    {
                        contractFilter = contracts
                            .Where(c => Contract.Equals(c.manifest.Name, StringComparison.OrdinalIgnoreCase))
                            .Select(c => c.hash)
                            .ToHashSet();
                        if (contractFilter.Count == 0) throw new Exception($"Couldn't resolve {Contract} contract");
                    }
                }

                IReadOnlySet<string>? eventFilter = null;
                if (!string.IsNullOrEmpty(EventName))
                {
                    eventFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EventName };
                }

                using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                using var _ = writer.WriteArray();
                var count = 0;

                await foreach (var (blockIndex, notification) in expressNode.EnumerateNotificationsAsync(contractFilter, eventFilter))
                {
                    if (Count.HasValue && count++ >= Count.Value) break;

                    using var __ = writer.WriteObject();
                    writer.WriteProperty("block-index", blockIndex);
                    writer.WriteProperty("script-hash", $"{notification.ScriptHash}");
                    if (contractMap.TryGetValue(notification.ScriptHash, out var name))
                    {
                        writer.WriteProperty("contract-name", name);
                    }
                    writer.WriteProperty("event-name", notification.EventName);
                    writer.WritePropertyName("state");
                    writer.WriteJson(Neo.VM.Helper.ToJson(notification.State)["value"]);
                }
            }
        }
    }
}
