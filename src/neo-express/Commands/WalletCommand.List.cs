using McMaster.Extensions.CommandLineUtils;
using System;

namespace Neo.Express.Commands
{
    internal partial class WalletCommand
    {
        [Command("list")]
        private class List
        {
            [Option]
            private string Input { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (devChain, _) = DevChain.Load(Input);
                    foreach (var wallet in devChain.Wallets)
                    {
                        console.WriteLine(wallet.Name);

                        foreach (var a in wallet.GetAccounts())
                        {
                            console.WriteLine($"    {a.Address}");
                            console.WriteLine($"    {a.ScriptHash}");
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
