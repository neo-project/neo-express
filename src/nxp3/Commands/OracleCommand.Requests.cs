using McMaster.Extensions.CommandLineUtils;
using System.Threading.Tasks;
using System;

namespace nxp3.Commands
{
    partial class OracleCommand
    {
        [Command("requests")]
        class Requests
        {
            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();
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
