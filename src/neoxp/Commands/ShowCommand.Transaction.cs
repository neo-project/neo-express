using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx", Description = "Show transaction")]
        class Transaction
        {
            readonly IBlockchainOperations blockchainOperations;

            public Transaction(IBlockchainOperations blockchainOperations)
            {
                this.blockchainOperations = blockchainOperations;
            }

            [Argument(0, Description = "Transaction hash")]
            [Required]
            string TransactionHash { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = blockchainOperations.LoadChain(Input);
                    // var (chain, _) = Program.LoadExpressChain(Input);
                    // var blockchainOperations = new BlockchainOperations();

                    // var (tx, log) = await blockchainOperations.ShowTransactionAsync(chain, TransactionHash).ConfigureAwait(false);
                    // console.WriteLine(tx.ToJson().ToString(true));
                    // if (log != null) console.WriteLine(log.ToJson().ToString(true));
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
