using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

using NeoExpress.Abstractions;
using System.Text;
using System;
using System.Text.Json;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "storage")]
        private class Storage
        {
            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            string Contract { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var blockchainOperations = new BlockchainOperations();
                    var storages = await blockchainOperations.GetStorages(chain, Contract);

                    if (Json)
                    {
                        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput(), new JsonWriterOptions() { Indented = true });
                        writer.WriteStartArray();
                        foreach (var storage in storages)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("key", storage.Key);
                            writer.WriteString("value", storage.Value);
                            writer.WriteBoolean("constant", storage.Constant);
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        foreach (var storage in storages)
                        {
                            console.WriteLine($"key:        0x{storage.Key}");
                            console.WriteLine($"  value:    0x{storage.Value}");
                            console.WriteLine($"  constant: {storage.Constant}");
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }

        }
    }
}
