using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    [Command("show")]
    [Subcommand(typeof(Account), 
        typeof(Claimable), 
        typeof(Coins), 
        typeof(Gas), 
        typeof(Transaction), 
        typeof(Unspents))]
    partial class ShowCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        [Command("account", Description = "show account state", ExtendedHelpText = @"
Remarks:
  For more info, please see https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getaccountstate.html")]
        private class Account
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new Neo2.BlockchainOperations();
                    await blockchainOperations.ShowAccount(chain, Name, Json, console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    return 1;
                }
            }
        }

        [Command("claimable", Description = "Show claimable GAS")]
        private class Claimable
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new Neo2.BlockchainOperations();
                    await blockchainOperations.ShowClaimable(chain, Name, Json, console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    return 1;
                }
            }
        }

        [Command("coins", Description = "show all UTXO asset transactions for an account")]
        private class Coins
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new Neo2.BlockchainOperations();
                    await blockchainOperations.ShowCoins(chain, Name, true, console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    return 1;
                }
            }
        }

        [Command("gas", "unclaimed", Description = "Show unclaimed GAS (alias \"unclaimed\")", ExtendedHelpText = @"
Remarks:
  Unavailable GAS can be converted to available GAS by transfering NEO.
  For more info, please see https://github.com/neo-project/neo-express/blob/master/docs/command-reference.md#neo-express-claim")]
        private class Gas
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new Neo2.BlockchainOperations();
                    await blockchainOperations.ShowGas(chain, Name, Json, console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    return 1;
                }
            }
        }

        [Command("unspents", "unspent", Description = "Show unspent assets (alias \"unspent\")")]
        private class Unspents
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new Neo2.BlockchainOperations();
                    await blockchainOperations.ShowUnspent(chain, Name, Json, console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    return 1;
                }
            }
        }
    }
}
