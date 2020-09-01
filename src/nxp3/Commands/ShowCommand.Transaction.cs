using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx")]
        class Transaction
        {
            [Argument(0)]
            private string TransactionHash { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();

                    var tx = await blockchainOperations.ShowTransaction(chain, TransactionHash).ConfigureAwait(false);
                    console.WriteLine(tx.ToJson().ToString(true));
                    // if (log != null)
                    // {
                    //     console.WriteLine(log.ToString(true));
                    // }
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
