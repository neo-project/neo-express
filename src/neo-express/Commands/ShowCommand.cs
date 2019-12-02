using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    [Command("show")]
    [Subcommand(typeof(Account), typeof(Claimable), typeof(Coins), typeof(Gas), typeof(Unspents))]
    class ShowCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        private static async Task<int> ExecuteAsync(CommandLineApplication app, IConsole console, string Name, string Input, Func<Uri, string, Task<JToken?>> func)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var account = chain.GetAccount(Name);
                if (account == null)
                {
                    throw new Exception($"{Name} wallet not found.");
                }

                var uri = chain.GetUri();
                var result = await func(uri, account.ScriptHash).ConfigureAwait(false);
                console.WriteResult(result);

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }

        [Command("account", Description="show account state", ExtendedHelpText = @"
Remarks:
  For more info, please see https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getaccountstate.html")]
        private class Account
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.GetAccountState);
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

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.GetClaimable);
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

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.ExpressShowCoins);
            }
        }

        [Command("gas", "unclaimed", Description="Show unclaimed GAS (alias \"unclaimed\")", ExtendedHelpText = @"
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

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.GetUnclaimed);
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

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.GetUnspents);
            }
        }
    }
}
