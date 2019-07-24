using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace Neo.Express.Commands
{
    internal partial class WalletCommand
    {
        [Command("delete")]
        private class Delete
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private bool Force { get; }

            [Option]
            private string Input { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (devChain, filename) = DevChain.Load(Input);
                    var wallet = devChain.GetWallet(Name);
                    if (wallet == default)
                    {
                        console.WriteLine($"{Name} privatenet wallet not found.");
                    }
                    else
                    {
                        if (!Force)
                        {
                            throw new Exception("You must specify force to delete a privatenet wallet.");
                        }

                        devChain.Wallets.Remove(wallet);
                        devChain.Save(filename);
                        console.WriteLine($"{Name} privatenet wallet deleted.");
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
