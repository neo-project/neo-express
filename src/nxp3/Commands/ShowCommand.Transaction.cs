using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx", Description = "Show transaction")]
        class Transaction
        {
            [Argument(0, Description = "Transaction hash")]
            [Required]
            string TransactionHash { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();

                    var (tx, log) = await blockchainOperations.ShowTransaction(chain, TransactionHash).ConfigureAwait(false);
                    console.WriteLine(tx.ToJson().ToString(true));
                    if (log != null) console.WriteLine(log.ToJson().ToString(true));
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
