using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;
using System;
using System.Linq;

namespace nxp3.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "list")]
        private class List
        {
            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var blockchainOperations = new BlockchainOperations();
                    var contracts = await blockchainOperations.ListContracts(chain)
                        .ConfigureAwait(false);

                    if (Json)
                    {
                        using var writer = new Newtonsoft.Json.JsonTextWriter(console.Out);
                        writer.WriteStartArray();
                        foreach (var (hash, manifest) in contracts)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("name");
                            writer.WriteValue(manifest.Name);
                            writer.WritePropertyName("hash");
                            writer.WriteValue(hash.ToString());
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        foreach (var (hash, manifest) in contracts)
                        {
                            console.WriteLine($"{manifest.Name} ({hash})");
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }

        }
    }
}
