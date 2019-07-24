using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;

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
                    var input = Program.DefaultPrivatenetFileName(Input);
                    if (!File.Exists(input))
                    {
                        throw new Exception($"{input} doesn't exist");
                    }

                    var devchain = DevChain.Load(input);
                    foreach (var wallet in devchain.Wallets)
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
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
