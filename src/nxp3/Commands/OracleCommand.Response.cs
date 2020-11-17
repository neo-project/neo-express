using System;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;
using Newtonsoft.Json.Linq;

namespace nxp3.Commands
{
    partial class OracleCommand
    {
        [Command("response")]
        class Response
        {
            [Argument(0)]
            string Url { get; } = string.Empty;

            [Argument(1)]
            string ResponsePath { get; } = string.Empty;

            [Option]
            (bool hasValue, ulong value) RequestId { get; }

            [Option]
            string Input { get; } = string.Empty;

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
                    var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();
                    var txs = await blockchainOperations.SubmitOracleResponse(chain, Url, OracleResponseCode.Success, responseJson, requestId)
                        .ConfigureAwait(false);

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
