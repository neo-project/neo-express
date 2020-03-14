using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo2.Models;
using System;
using System.Linq;

namespace NeoExpress.Commands
{
    internal partial class WalletCommand
    {
        [Command("list")]
        private class List
        {
            [Option]
            private string Input { get; } = string.Empty;

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    foreach (var wallet in chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                    {
                        console.WriteLine(wallet.Name);

                        foreach (var account in wallet.Accounts)
                        {
                            console.WriteLine($"    {account.ScriptHash}");
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
