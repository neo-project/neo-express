using System;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("delete", Description = "Delete neo-express wallet")]
        class Delete
        {
            [Argument(0, Description = "Wallet name")]
            [Required]
            string Name { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            bool Force { get; }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal int OnExecute(CommandLineApplication app, IConsole console)
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
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
