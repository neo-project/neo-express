using System;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    partial class OracleCommand
    {
        [Command("enable")]
        class Enable
        {
            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();
                    var txHash = await blockchainOperations
                        .DesignateOracleRoles(chain, chain.ConsensusNodes.Select(n => n.Wallet.DefaultAccount))
                        .ConfigureAwait(false);
                    console.WriteLine($"Oracle Enable Transaction {txHash} submitted");

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
