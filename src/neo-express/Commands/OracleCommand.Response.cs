using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;
using Newtonsoft.Json.Linq;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("response", Description = "Submit oracle response")]
        class Response
        {
            [Argument(0, Description = "URL of oracle request")]
            string Url { get; } = string.Empty;

            [Argument(1, Description = "Path to JSON file with oracle response cotnent")]
            string ResponsePath { get; } = string.Empty;

            [Option(Description = "Oracle request ID")]
            (bool hasValue, ulong value) RequestId { get; }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if (!File.Exists(ResponsePath))
                    {
                        throw new Exception($"Response File {ResponsePath} couldn't be found");
                    }

                    JObject responseJson;
                    {
                        using var stream = File.OpenRead(ResponsePath);
                        using var reader = new StreamReader(stream);
                        using var jsonReader = new Newtonsoft.Json.JsonTextReader(reader);
                        responseJson = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
                    }

                    var requestId = RequestId.hasValue ? (ulong?)RequestId.value : null;

                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();
                    var txs = await blockchainOperations.SubmitOracleResponse(chain, Url, OracleResponseCode.Success, responseJson, requestId)
                        .ConfigureAwait(false);

                    if (Json)
                    {
                        using var writer = new Newtonsoft.Json.JsonTextWriter(console.Out);
                        writer.WriteStartArray();
                        for (int i = 0; i < txs.Count; i++)
                        {
                            writer.WriteValue(txs[i].ToString());
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        if (txs.Count == 0)
                        {
                            console.WriteLine("No oracle response transactions submitted");
                        }
                        else
                        {
                            console.WriteLine("Oracle response transactions submitted:");
                            for (int i = 0; i < txs.Count; i++)
                            {
                                console.WriteLine($"    {txs[i]}");
                            }
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
