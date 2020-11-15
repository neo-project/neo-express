using System;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;

namespace nxp3.Commands
{
    partial class OracleCommand
    {
        [Command("disable")]
        class Disable
        {
            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    throw new NotImplementedException();
                    // var (chain, _) = Program.LoadExpressChain(Input);
                    // var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();
                    // var txHash = await blockchainOperations
                    //     .DesignateOracleRoles(chain, Enumerable.Empty<ExpressWalletAccount>())
                    //     .ConfigureAwait(false);
                    // console.WriteLine($"Oracle Disable Transaction {txHash} submitted");
                    // return 0;
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
