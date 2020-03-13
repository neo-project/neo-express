using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Neo2.Commands
{
    internal partial class WalletCommand
    {
        [Command("delete")]
        private class Delete
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private bool Force { get; }

            [Option]
            private string Input { get; } = string.Empty;

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    var wallet = chain.GetWallet(Name);

                    if (wallet == null)
                    {
                        console.WriteLine($"{Name} privatenet wallet not found.");
                    }
                    else
                    {
                        if (!Force)
                        {
                            throw new Exception("You must specify force to delete a privatenet wallet.");
                        }

                        chain.Wallets.Remove(wallet);
                        chain.Save(filename);
                        console.WriteLine($"{Name} privatenet wallet deleted.");
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
