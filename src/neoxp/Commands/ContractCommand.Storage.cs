using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.SmartContract.Manifest;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "storage", Description = "Display storage for specified contract")]
        internal class Storage
        {
            readonly IExpressChain chain;

            public Storage(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Storage(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; }

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                if (UInt160.TryParse(Contract, out var hash))
                {
                    var storages = await expressNode.ListStoragesAsync(hash).ConfigureAwait(false);
                    if (Json)
                    {
                        using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                        WriteJsonStorages(writer, storages);
                    }
                    else
                    {
                        WriteStorages(console, hash, storages);
                    }
                }
                else
                {
                    var contracts = await expressNode.ListContractsByNameAsync(Contract).ConfigureAwait(false);
                    if (Json)
                    {
                        using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                        using var _ = writer.WriteArray();
                        for (int i = 0; i < contracts.Count; i++)
                        {
                            var storages = await expressNode.ListStoragesAsync(contracts[i].hash).ConfigureAwait(false);
                            using var __ = writer.WriteObject();
                            writer.WriteProperty("script-hash", $"{contracts[i].hash}");
                            writer.WritePropertyName("storages");
                            WriteJsonStorages(writer, storages);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < contracts.Count; i++)
                        {
                            var storages = await expressNode.ListStoragesAsync(contracts[i].hash).ConfigureAwait(false);
                            WriteStorages(console, contracts[i].hash, storages);
                        }
                    }
                }

                static void WriteJsonStorages(JsonWriter writer, IReadOnlyList<(string key, string value)> storages)
                {
                    using var _ = writer.WriteArray();
                    for (int i = 0; i < storages.Count; i++)
                    {
                        var (key, value) = storages[i];
                        using var __ = writer.WriteObject();
                        writer.WriteProperty("key", "0x" + key);
                        writer.WriteProperty("value", "0x" + value);
                    }
                }

                static void WriteStorages(IConsole console, UInt160 hash, IReadOnlyList<(string key, string value)> storages)
                {
                    if (storages.Count == 0)
                    {
                        console.WriteLine($"There are no storage records for {hash}");
                    }
                    else
                    {
                        console.WriteLine($"{hash} storage records:");
                        for (int i = 0; i < storages.Count; i++)
                        {
                            var (key, value) = storages[i];
                            console.WriteLine($"  key:     0x{key}");
                            console.WriteLine($"    value: 0x{value}");
                        }
                    }
                }
            }
        }
    }
}
