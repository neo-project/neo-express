using System;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("enable", Description = "Enable oracles for neo-express instance")]
        class Enable
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
                    var txHash = await blockchainOperations
                        .DesignateOracleRoles(chain, chain.ConsensusNodes.Select(n => n.Wallet.DefaultAccount))
                        .ConfigureAwait(false);
                    console.WriteLine($"Oracle Enable Transaction {txHash} submitted");

                    if (Json)
                    {
                        console.WriteLine($"{txHash}");
                    }
                    else
                    {
                        console.WriteLine($"Transfer Transaction {txHash} submitted");
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
