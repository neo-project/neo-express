using McMaster.Extensions.CommandLineUtils;
using System.Threading.Tasks;
using System;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("requests", Description = "List outstanding oracle requests")]
        class Requests
        {
            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();
                    var requests = await blockchainOperations
                        .GetOracleRequests(chain)
                        .ConfigureAwait(false);

                    foreach (var (id, request) in requests)
                    {
                        console.WriteLine($"request #{id}:");
                        console.WriteLine($"    Original Tx Hash: {request.OriginalTxid}");
                        console.WriteLine($"    Request Url:      \"{request.Url}\"");
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
