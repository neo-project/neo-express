using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    [Command("show")]
    [Subcommand(typeof(Account), typeof(Coins), typeof(Gas))]
    class ShowCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        private static async Task<int> ExecuteAsync(CommandLineApplication app, IConsole console, string Name, string Input, Func<Uri, string, Task<JToken>> func)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var account = chain.GetAccount(Name);
                if (account == default)
                {
                    throw new Exception($"{Name} wallet not found.");
                }

                var uri = chain.GetUri();
                var result = await func(uri, account.ScriptHash).ConfigureAwait(false);
                console.WriteLine(result.ToString(Formatting.Indented));

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
            private string Name { get; }

            [Option]
            private string Input { get; }

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.GetAccountState);
            }
        }

        [Command("coins")]
        private class Coins
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private string Input { get; }

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.ExpressShowCoins);
            }
        }

        [Command("gas")]
        private class Gas
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private string Input { get; }

            private Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                return ExecuteAsync(app, console, Name, Input, NeoRpcClient.ExpressShowGas);
            }
        }
    }
}
