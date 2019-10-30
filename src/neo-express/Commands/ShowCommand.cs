using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    [Command("show")]
    [Subcommand(typeof(Account), typeof(Claimable), typeof(Coins), typeof(Unclaimed), typeof(Unspents))]
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

        [Command("account")]
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

        [Command("claimable")]
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

        [Command("coins")]
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

        [Command("unclaimed")]
        private class Unclaimed
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

        [Command("unspents")]
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
